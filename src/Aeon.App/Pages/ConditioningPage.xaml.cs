using Aeon.App.Models;
using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App.Pages;

/// <summary>Conditioning protocols grouped by sport, then by modality.</summary>
public partial class ConditioningPage : ContentPage
{
    private readonly BundleService _bundle;

    public ConditioningPage(BundleService bundle)
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
        var rows = _bundle.Data!.Conditioning
            .GroupBy(p => p.Sport)
            .Select(g => new ConditioningSportRow(g.Key, g.Count()))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
        SportsList.ItemsSource = rows;
        FooterLabel.Text = $"{rows.Count} sports · {_bundle.Data.Conditioning.Count} protocols";
    }

    private async void OnSportSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ConditioningSportRow row)
        {
            return;
        }

        SportsList.SelectedItem = null;
        await Navigation.PushAsync(new ModalitiesPage(_bundle, row.Name));
    }
}