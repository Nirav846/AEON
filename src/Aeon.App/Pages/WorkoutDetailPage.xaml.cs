using Aeon.App.Models;
using Aeon.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Aeon.App.Pages;

/// <summary>One workout: header, ordered exercise cards (tap a card to edit its
/// freeform prescription or remove it), and export/delete actions. Every render
/// resolves entries live against the current bundle, so what's on screen is always
/// the current text of the source record — never a stale copy.</summary>
public partial class WorkoutDetailPage : ContentPage
{
    /// <summary>Accent shared with the other pages, resolved from the resource
    /// dictionary rather than hard-coded.</summary>
    private static Color AccentColor
        => Application.Current?.Resources.TryGetValue("Accent", out var v) == true && v is Color c
            ? c
            : Color.FromArgb("#8B7CF6");

    private readonly WorkoutStore _store;
    private readonly BundleService _bundle;
    private readonly Workout _workout;

    public WorkoutDetailPage(WorkoutStore store, BundleService bundle, Workout workout)
    {
        InitializeComponent();
        _store = store;
        _bundle = bundle;
        _workout = workout;
        Title = workout.Name;
        Build();
    }

    private void Build()
    {
        Body.Clear();

        Body.Add(new Label
        {
            Text = _workout.Name,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
        });
        if (!string.IsNullOrWhiteSpace(_workout.Meta))
        {
            Body.Add(new Label
            {
                Text = _workout.Meta,
                FontSize = 13,
                TextColor = Colors.Gray,
            });
        }

        Body.Add(new Label
        {
            Text = "EXERCISES",
            TextColor = AccentColor,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.1,
            Margin = new Thickness(0, 14, 0, 2),
        });

        if (_workout.Entries.Count == 0)
        {
            Body.Add(new Label
            {
                Text = "Nothing yet. Open a complex or conditioning protocol from Browse, "
                     + "Conditioning or Search, then tap Add to workout.",
                FontSize = 14,
                TextColor = Colors.Gray,
                LineBreakMode = LineBreakMode.WordWrap,
            });
            return;
        }

        for (var i = 0; i < _workout.Entries.Count; i++)
        {
            var index = i;
            var entry = _workout.Entries[i];
            var view = WorkoutText.Resolve(_bundle.Data, entry);

            var stack = new VerticalStackLayout { Spacing = 3 };
            stack.Add(new Label
            {
                Text = $"{index + 1}. {view.Name}",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
            });
            stack.Add(new Label
            {
                Text = view.Meta,
                FontSize = 11,
                TextColor = Colors.Gray,
            });

            // Prescription is the one field Nirav owns — never pulled from the bundle.
            var rx = string.IsNullOrWhiteSpace(entry.Prescription) ? null : entry.Prescription;
            stack.Add(new Label
            {
                Text = rx is null ? "Rx: tap to add prescription" : $"Rx: {rx}",
                FontSize = 13,
                TextColor = rx is null ? Colors.Gray : Colors.Black,
                LineBreakMode = LineBreakMode.WordWrap,
            });

            var card = new Border
            {
                Stroke = Colors.LightGray,
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(14, 12),
                Margin = new Thickness(0, 4),
                Content = stack,
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OnEntryTappedAsync(index);
            card.GestureRecognizers.Add(tap);
            Body.Add(card);
        }
    }

    private async Task OnEntryTappedAsync(int index)
    {
        var entry = _workout.Entries[index];
        var view = WorkoutText.Resolve(_bundle.Data, entry);
        var choice = await DisplayActionSheetAsync(
            $"{index + 1}. {view.Name}", "Cancel", null, "Edit prescription", "Remove from workout");

        if (choice == "Edit prescription")
        {
            var text = await DisplayPromptAsync(
                "Prescription",
                "Type it as you'd say it on the floor",
                "Save",
                "Cancel",
                "e.g. 4×5 @ 80% · 90s rest",
                initialValue: entry.Prescription);
            if (text is null)
            {
                return;
            }
            try
            {
                await _store.SetPrescriptionAsync(_workout, index, text);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Not saved", ex.Message, "OK");
            }
            Build();
        }
        else if (choice == "Remove from workout")
        {
            try
            {
                await _store.RemoveEntryAsync(_workout, index);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Not saved", ex.Message, "OK");
            }
            Build();
        }
    }

    private async void OnEditDetails(object? sender, EventArgs e)
    {
        // Cancel on either prompt aborts the whole edit — stated in the meta hint so
        // "back out of the second box" is never mistaken for "save what I typed so far".
        var name = await DisplayPromptAsync(
            "Workout name", "Shown at the top of the sheet", "Next", "Cancel",
            initialValue: _workout.Name);
        if (name is null)
        {
            return;
        }
        var meta = await DisplayPromptAsync(
            "Meta line",
            "Context under the name, e.g. 2026-10-15 · Wk 3. Blank removes it; Cancel aborts the edit.",
            "Save",
            "Cancel",
            initialValue: _workout.Meta);
        if (meta is null)
        {
            return;
        }
        try
        {
            await _store.RenameAsync(_workout, name, meta);
            Title = _workout.Name;
            Build();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Not saved", ex.Message, "OK");
        }
    }

    private async void OnExport(object? sender, EventArgs e)
    {
        if (_workout.Entries.Count == 0)
        {
            await DisplayAlertAsync("Nothing to export", "Add at least one exercise first.", "OK");
            return;
        }
        try
        {
            ExportButton.IsEnabled = false;
            var path = await WorkoutPdf.ExportAsync(_workout, _bundle.Data);
            await WorkoutPdf.ShareAsync(path);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Export failed", ex.Message, "OK");
        }
        finally
        {
            ExportButton.IsEnabled = true;
        }
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            "Delete workout", $"Delete \"{_workout.Name}\"? This cannot be undone.", "Delete", "Cancel");
        if (!confirmed)
        {
            return;
        }
        try
        {
            await _store.DeleteAsync(_workout);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Not saved", ex.Message, "OK");
        }
    }
}
