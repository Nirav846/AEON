using Aeon.App.Models;
using Aeon.App.Services;

namespace Aeon.App.Pages;

/// <summary>Fourth tab: every workout Nirav has authored on this device, newest
/// edited first. Nothing here reads the bundle — workouts are local-first; the
/// bundle only comes in when an entry is resolved for display or export.</summary>
public partial class WorkoutsPage : ContentPage
{
    private readonly BundleService _bundle;
    private readonly WorkoutStore _store;

    public WorkoutsPage(BundleService bundle, WorkoutStore store)
    {
        InitializeComponent();
        _bundle = bundle;
        _store = store;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Once per process — the store must not reload the file underneath pages
            // that are holding references into it.
            await _store.LoadAsync();
        }
        catch (Exception ex)
        {
            FooterLabel.Text = $"Load failed: {ex.Message}";
            return;
        }
        Populate();
    }

    private void Populate()
    {
        var rows = _store.Workouts
            .OrderByDescending(w => w.UpdatedUtc)
            .Select(w => new WorkoutRow(w))
            .ToList();
        WorkoutsList.ItemsSource = rows;
        EmptyLabel.IsVisible = rows.Count == 0;

        var footer = $"{rows.Count} workout{(rows.Count == 1 ? "" : "s")} stored on this device";
        if (!string.IsNullOrWhiteSpace(_store.Notice))
        {
            footer += $" · {_store.Notice}";
        }
        FooterLabel.Text = footer;
    }

    private async void OnNewWorkout(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("New workout", "Name (e.g. Tuesday Power)", "Create", "Cancel");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        try
        {
            var workout = await _store.CreateAsync(name, "");
            await Navigation.PushAsync(new WorkoutDetailPage(_store, _bundle, workout));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Not saved", ex.Message, "OK");
        }
        Populate();
    }

    private async void OnWorkoutSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not WorkoutRow row)
        {
            return;
        }
        WorkoutsList.SelectedItem = null;
        await Navigation.PushAsync(new WorkoutDetailPage(_store, _bundle, row.Workout));
    }
}