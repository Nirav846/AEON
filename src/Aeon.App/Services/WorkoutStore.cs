using System.Text.Json;
using Aeon.App.Models;

namespace Aeon.App.Services;

/// <summary>
/// The app's first local-write mechanism: a single versioned JSON file holding every
/// workout, in FileSystem.AppDataDirectory (app-private, single user, no sync).
///
/// Write discipline — the file Nirav authored is his only copy, so every save:
///   1. serializes to workouts.json.tmp;
///   2. re-parses the tmp bytes and checks the workout count round-trips — a bad write
///      never gets far enough to displace the live file;
///   3. copies the live file to workouts.json.bak (previous known-good becomes backup);
///   4. renames tmp over the live file (same-volume rename = atomic on Android).
///
/// Read discipline — a bad file is never overwritten in place. On load:
///   - unreadable main -> renamed to a timestamped .corrupt-* archive, then .bak tried;
///   - newer schema    -> renamed to a timestamped .v&lt;N&gt;-* archive, then .bak tried;
///   - nothing usable  -> start empty, with a Notice explaining what happened, which
///     the Workouts tab prints in its footer rather than hiding.
/// </summary>
public sealed class WorkoutStore
{
    public const int CurrentSchemaVersion = 1;
    private const string FileName = "workouts.json";

    private readonly string _main;
    private readonly string _tmp;
    private readonly string _bak;
    private readonly SemaphoreSlim _gate = new(1, 1);
    // Relaxed encoder: this is a local file a human may open, and prescriptions like
    // "4×5 @ 80% · 90s rest" should read as themselves, not \u00d7. Valid JSON either way.
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private readonly List<Workout> _workouts = new();
    private bool _loaded;

    public WorkoutStore() : this(FileSystem.AppDataDirectory)
    {
    }

    /// <summary>Directory-taking ctor so storage behaviour can be exercised outside
    /// the app (store harness); the app itself always uses app-private storage.</summary>
    public WorkoutStore(string directory)
    {
        _main = Path.Combine(directory, FileName);
        _tmp = _main + ".tmp";
        _bak = _main + ".bak";
    }

    public IReadOnlyList<Workout> Workouts => _workouts;

    /// <summary>Non-null when loading had to repair, archive or recover something.
    /// Surfaced in the tab footer — the app does not silently swallow a data event.</summary>
    public string? Notice { get; private set; }

    public async Task LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            _workouts.Clear();
            Notice = null;

            var doc = await TryReadAsync(_main);

            if (doc is { SchemaVersion: > CurrentSchemaVersion })
            {
                var arch = await ArchiveAsync(_main, $"v{doc.SchemaVersion}");
                Notice = $"file from a newer app version — kept as {Path.GetFileName(arch)}";
                doc = null;
            }
            else if (doc is null && File.Exists(_main))
            {
                var arch = await ArchiveAsync(_main, "corrupt");
                Notice = $"unreadable workouts.json — kept as {Path.GetFileName(arch)}";
            }

            if (doc is null && File.Exists(_bak))
            {
                var bak = await TryReadAsync(_bak);
                if (bak is { SchemaVersion: <= CurrentSchemaVersion })
                {
                    doc = bak;
                    Notice = Notice is null ? "restored from backup" : Notice + " · restored from backup";
                }
                else
                {
                    Notice = Notice is null ? "backup unreadable — starting empty"
                                            : Notice + " · backup unreadable";
                }
            }

            if (doc?.Workouts is not null)
            {
                foreach (var w in doc.Workouts)
                {
                    w.Entries ??= new List<WorkoutEntry>();
                    _workouts.Add(w);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Workout> CreateAsync(string name, string meta)
    {
        await _gate.WaitAsync();
        try
        {
            var workout = new Workout { Name = name.Trim(), Meta = meta.Trim() };
            _workouts.Add(workout);
            await SaveOrRollbackAsync(null, () => _workouts.Remove(workout));
            return workout;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RenameAsync(Workout workout, string name, string meta)
    {
        await _gate.WaitAsync();
        try
        {
            var (prevName, prevMeta) = (workout.Name, workout.Meta);
            workout.Name = name.Trim();
            workout.Meta = meta.Trim();
            await SaveOrRollbackAsync(workout, () => { workout.Name = prevName; workout.Meta = prevMeta; });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddEntryAsync(Workout workout, WorkoutEntry entry)
    {
        await _gate.WaitAsync();
        try
        {
            workout.Entries.Add(entry);
            await SaveOrRollbackAsync(workout, () => workout.Entries.Remove(entry));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetPrescriptionAsync(Workout workout, int index, string prescription)
    {
        await _gate.WaitAsync();
        try
        {
            var entry = workout.Entries[index];
            var prev = entry.Prescription;
            entry.Prescription = prescription.Trim();
            await SaveOrRollbackAsync(workout, () => entry.Prescription = prev);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveEntryAsync(Workout workout, int index)
    {
        await _gate.WaitAsync();
        try
        {
            var entry = workout.Entries[index];
            workout.Entries.RemoveAt(index);
            await SaveOrRollbackAsync(workout, () => workout.Entries.Insert(index, entry));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(Workout workout)
    {
        await _gate.WaitAsync();
        try
        {
            var index = _workouts.IndexOf(workout);
            if (index < 0)
            {
                return;
            }
            _workouts.RemoveAt(index);
            await SaveOrRollbackAsync(null, () => _workouts.Insert(index, workout));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Bump the touched workout's timestamp, persist, and undo everything if
    /// the save throws — in-memory state must never run ahead of what is on disk,
    /// because the user would otherwise see an edit a relaunch silently reverts.
    /// Field mutations are undone by the caller's closure; the timestamp is undone here.</summary>
    private async Task SaveOrRollbackAsync(Workout? touched, Action undo)
    {
        var prevStamp = touched?.UpdatedUtc;
        if (touched is not null)
        {
            touched.UpdatedUtc = DateTimeOffset.UtcNow;
        }
        try
        {
            await PersistAsync();
        }
        catch
        {
            if (touched is not null && prevStamp is not null)
            {
                touched.UpdatedUtc = prevStamp.Value;
            }
            undo();
            throw;
        }
    }

    /// <summary>tmp -> verify -> backup -> atomic rename-over, as documented on the class.</summary>
    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(
            new WorkoutDocument { SchemaVersion = CurrentSchemaVersion, Workouts = _workouts }, _json);
        await File.WriteAllTextAsync(_tmp, json);

        var check = JsonSerializer.Deserialize<WorkoutDocument>(await File.ReadAllTextAsync(_tmp));
        if (check?.Workouts is null
            || check.SchemaVersion != CurrentSchemaVersion
            || check.Workouts.Count != _workouts.Count)
        {
            try { File.Delete(_tmp); } catch (IOException) { }
            throw new IOException("workouts.json failed verification after write — existing file untouched");
        }

        if (File.Exists(_main))
        {
            File.Copy(_main, _bak, overwrite: true);
        }
        File.Move(_tmp, _main, overwrite: true);
    }

    private async Task<WorkoutDocument?> TryReadAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var doc = JsonSerializer.Deserialize<WorkoutDocument>(await File.ReadAllTextAsync(path));
            return doc?.Workouts is null ? null : doc;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Move-aside, never overwrite: if an archive with the same tag already
    /// exists it is kept and this one lands next to it with a counter suffix.</summary>
    private Task<string> ArchiveAsync(string path, string tag)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = $"{path}.{tag}-{stamp}";
        var suffix = 0;
        while (File.Exists(target))
        {
            target = $"{path}.{tag}-{stamp}-{++suffix}";
        }
        File.Move(path, target);
        return Task.FromResult(target);
    }
}