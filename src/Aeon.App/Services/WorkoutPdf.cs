using System.Text;
using System.Text.RegularExpressions;
using Aeon.App.Models;
using Aeon.Data;
using FossPDF.Fluent;
using FossPDF.Helpers;
using FossPDF.Infrastructure;
using Microsoft.Maui.ApplicationModel.DataTransfer;
// MAUI (implicit usings) and FossPDF both export a `Colors`; inside this file every
// color is a PDF color (grey meta/swap lines, white header text), so bind the simple
// name to FossPDF's rather than qualifying all seven call sites.
using Colors = FossPDF.Helpers.Colors;

namespace Aeon.App.Services;

/// <summary>
/// Generates the workout sheet and hands it to the system share sheet. FossPDF (MIT) —
/// the library the on-device PoC validated; parked fallback order, should it ever
/// regress: old QuestPDF, then a hand-rolled minimal writer, then iText (AGPL) last.
///
/// Layout, as ruled: workout name + meta header (repeats on every page), then per
/// entry — name, meta, one freeform prescription line, the record's coach cue, and
/// the swap line in gray as the on-the-floor fallback.
/// </summary>
public static class WorkoutPdf
{
    private static readonly SemaphoreSlim FontGate = new(1, 1);
    private static bool _fontsRegistered;

    /// <summary>Generate into FileSystem.CacheDirectory (derived output — workouts.json
    /// remains the only source of truth) and verify the bytes that actually landed.
    /// Returns the file path.</summary>
    public static async Task<string> ExportAsync(Workout workout, Bundle? bundle)
    {
        await EnsureFontsAsync();

        var path = Path.Combine(FileSystem.CacheDirectory, FileName(workout.Name));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var rows = workout.Entries
            .Select(e => (Entry: e, View: WorkoutText.Resolve(bundle, e)))
            .ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("OpenSans").FontSize(11));

                page.Header().Column(h =>
                {
                    h.Item().Text(workout.Name).FontFamily("OpenSansSemi").FontSize(20);
                    if (!string.IsNullOrWhiteSpace(workout.Meta))
                    {
                        h.Item().Text(workout.Meta).FontSize(11).FontColor(Colors.Grey.Medium);
                    }
                    h.Item().Text($"AEON · exported {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });

                // One column for the whole document: FossPDF flows it across pages
                // automatically (verified locally — 120 blocks paginated to 15 pages).
                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var (entry, view) = rows[i];
                        col.Item().Column(item =>
                        {
                            item.Spacing(2);
                            item.Item().Text($"{i + 1}. {view.Name}").FontFamily("OpenSansSemi").FontSize(14);
                            item.Item().Text(view.Meta).FontSize(10).FontColor(Colors.Grey.Medium);
                            item.Item().Text(t =>
                            {
                                t.Span("Prescription ").Bold();
                                if (string.IsNullOrWhiteSpace(entry.Prescription))
                                {
                                    t.Span("—").FontColor(Colors.Grey.Medium);
                                }
                                else
                                {
                                    t.Span(entry.Prescription);
                                }
                            });
                            if (view.Cue is not null)
                            {
                                item.Item().Text(t => { t.Span("Cue ").Bold(); t.Span(view.Cue!); });
                            }
                            if (view.Swap is not null)
                            {
                                // Ruled in as the one-line gray fallback: if the floor
                                // doesn't have the primary tool today, the answer is here.
                                item.Item().Text(t =>
                                {
                                    t.Span("Swap ").FontColor(Colors.Grey.Medium);
                                    t.Span(view.Swap!).FontColor(Colors.Grey.Medium);
                                });
                            }
                        });
                    }
                });
            });
        }).GeneratePdf(path);

        // What actually hit disk, not what we hope did (PoC discipline).
        var bytes = await File.ReadAllBytesAsync(path);
        var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length));
        if (bytes.Length < 1000 || header != "%PDF-")
        {
            throw new IOException($"generated file is not a valid PDF (length={bytes.Length}, header=\"{header}\")");
        }
        var pages = Regex.Matches(Encoding.ASCII.GetString(bytes), @"/Type\s*/Page[^s]").Count;
        Android.Util.Log.Info("AeonPdf", $"exported {path} length={bytes.Length} pages={pages}");
        return path;
    }

    public static Task ShareAsync(string path)
        => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Share workout",
            File = new ShareFile(path),
        });

    /// <summary>Fonts come from the APK, not the platform, so the sheet renders
    /// identically on every device. Registered once per process.</summary>
    private static async Task EnsureFontsAsync()
    {
        if (_fontsRegistered)
        {
            return;
        }
        await FontGate.WaitAsync();
        try
        {
            if (_fontsRegistered)
            {
                return;
            }
            await using var regular = await FileSystem.OpenAppPackageFileAsync("OpenSans-Regular.ttf");
            FossPDF.Drawing.FontManager.RegisterFontWithCustomName("OpenSans", regular);
            await using var semibold = await FileSystem.OpenAppPackageFileAsync("OpenSans-Semibold.ttf");
            FossPDF.Drawing.FontManager.RegisterFontWithCustomName("OpenSansSemi", semibold);
            _fontsRegistered = true;
        }
        finally
        {
            FontGate.Release();
        }
    }

    /// <summary>&lt;sanitized name&gt;-&lt;yyyy-MM-dd&gt;.pdf — readable in a share target's
    /// file list, safe on Android's filesystem; same-day re-exports replace (the PDF is
    /// derived output, workouts.json is the source of truth).</summary>
    private static string FileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();
        foreach (var raw in string.IsNullOrWhiteSpace(name) ? "workout" : name.Trim())
        {
            var ch = raw switch
            {
                ' ' or '\t' => '-',
                _ when Array.IndexOf(invalid, raw) >= 0 => '_',
                _ => raw,
            };
            sb.Append(ch);
        }
        var safe = sb.ToString();
        while (safe.Contains("--"))
        {
            safe = safe.Replace("--", "-");
        }
        safe = safe.Trim('-', '_');
        if (safe.Length > 60)
        {
            safe = safe[..60].TrimEnd('-');
        }
        if (safe.Length == 0)
        {
            safe = "workout";
        }
        return $"{safe}-{DateTime.Now:yyyy-MM-dd}.pdf";
    }
}
