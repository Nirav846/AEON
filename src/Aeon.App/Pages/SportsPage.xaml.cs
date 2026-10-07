using Aeon.App.Models;
using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App.Pages;

public partial class SportsPage : ContentPage
{
    private readonly BundleService _bundle;

    public SportsPage(BundleService bundle)
    {
        InitializeComponent();
        _bundle = bundle;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_bundle.Data is not null)
        {
            Populate();
            return;
        }

        Loading.IsVisible = Loading.IsRunning = true;
        await _bundle.LoadAsync();
        Loading.IsVisible = Loading.IsRunning = false;

        if (_bundle.Data is null)
        {
            FooterLabel.Text = $"No data: {_bundle.Error}";
            return;
        }

        Populate();
    }

    private void Populate()
    {
        var rows = _bundle.Data!.Complexes
            .GroupBy(c => c.Sport)
            .Select(g => new SportRow(g.Key, g.Count()))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
        SportsList.ItemsSource = rows;

        var source = _bundle.ServedFromEmbeddedFallback
            ? "bundled copy (cache unusable)"
            : _bundle.Status == BundleLoadStatus.FromCacheOffline
                ? "offline-cache"
                : _bundle.Status?.ToString();

        FooterLabel.Text =
            $"bundle {_bundle.Version} · {_bundle.Data!.GeneratedAt:yyyy-MM-dd} · {source}";
    }

    private async void OnSportSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SportRow row)
        {
            return;
        }

        SportsList.SelectedItem = null;
        await Navigation.PushAsync(new RolesPage(_bundle, row.Name));
    }
}