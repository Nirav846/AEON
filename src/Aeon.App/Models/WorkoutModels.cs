using System.Text.Json.Serialization;
using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App.Models;

/// <summary>
/// The app's first user-authored, user-persisted record type. Everything else the
/// app shows is read-only bundle data; this is the one shape Nirav writes himself,
/// stored in a single versioned JSON file (see WorkoutStore).
/// </summary>
public sealed class WorkoutDocument
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; } = WorkoutStore.CurrentSchemaVersion;
    [JsonPropertyName("workouts")] public List<Workout> Workouts { get; set; } = new();
}

public sealed class Workout
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    /// <summary>Freeform context line printed under the workout name on the sheet,
    /// e.g. "2026-10-15 · Wk 3 · heavy day". Optional; not a structured field.</summary>
    [JsonPropertyName("meta")] public string Meta { get; set; } = "";
    [JsonPropertyName("created_utc")] public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    [JsonPropertyName("updated_utc")] public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    [JsonPropertyName("entries")] public List<WorkoutEntry> Entries { get; set; } = new();
}

/// <summary>
/// One exercise slot in a workout. Deliberately a REFERENCE plus a name snapshot,
/// never a denormalized copy of the source record:
///   - ref_type + ref_id resolve live against the current bundle at render time, so
///     cue/swap/meta text stays current as records get fixed or superseded;
///   - name_snapshot keeps the row readable if the source record later disappears
///     from the bundle, instead of leaving a dangling blank.
/// Prescription is freeform on purpose — AEON's database stores no sets/reps/rest,
/// so the number is typed at build time, and one field fits both "4×5 @ 80%" and
/// "30s on / 30s off × 8" without imposing a structure conditioning work doesn't have.
/// </summary>
public sealed class WorkoutEntry
{
    public const string RefComplex = "complex";
    public const string RefConditioning = "conditioning";

    [JsonPropertyName("ref_type")] public string RefType { get; set; } = RefComplex;
    [JsonPropertyName("ref_id")] public int RefId { get; set; }
    [JsonPropertyName("name_snapshot")] public string NameSnapshot { get; set; } = "";
    [JsonPropertyName("prescription")] public string Prescription { get; set; } = "";
}

/// <summary>The bundle data an entry currently renders as — name/meta/cue/swap resolved
/// live, with IsLive=false when the source record is gone and the snapshot is all that's left.</summary>
public sealed record ResolvedEntry(string Name, string Meta, string? Cue, string? Swap, bool IsLive);

/// <summary>Resolves a stored reference against a bundle. Shared by the workout editor
/// and the PDF writer so the screen and the sheet can never disagree about what an
/// entry means.</summary>
public static class WorkoutText
{
    public static ResolvedEntry Resolve(Bundle? bundle, WorkoutEntry entry)
    {
        if (bundle is not null && entry.RefType == WorkoutEntry.RefConditioning)
        {
            var p = bundle.Conditioning.FirstOrDefault(x => x.Id == entry.RefId);
            if (p is not null)
            {
                // Same fields ProtocolDetailPage leads with: sport, modality, work:rest.
                return new ResolvedEntry(p.Name, $"{p.Sport} · {p.Modality} · {p.WorkToRest}", null, null, true);
            }
        }
        else if (bundle is not null)
        {
            var c = bundle.Complexes.FirstOrDefault(x => x.Id == entry.RefId);
            if (c is not null)
            {
                var cue = string.IsNullOrWhiteSpace(c.Manual?.Cue) ? null : c.Manual!.Cue;
                var swap = string.IsNullOrWhiteSpace(c.Swap) ? null : c.Swap;
                return new ResolvedEntry(c.Name, $"{c.Sport} · {c.Role} · {c.Category}", cue, swap, true);
            }
        }

        return new ResolvedEntry(entry.NameSnapshot, "not in the current bundle", null, null, false);
    }
}

/// <summary>Display row for the Workouts tab list.</summary>
public sealed record WorkoutRow(Workout Workout)
{
    public string Name => Workout.Name;

    public string Detail
    {
        get
        {
            var count = $"{Workout.Entries.Count} exercise{(Workout.Entries.Count == 1 ? "" : "s")}";
            return string.IsNullOrWhiteSpace(Workout.Meta) ? count : $"{Workout.Meta} · {count}";
        }
    }
}