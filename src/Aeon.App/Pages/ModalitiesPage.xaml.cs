using Aeon.App.Models;
using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App.Pages;

/// <summary>Modalities within one conditioning sport.</summary>
public partial class ModalitiesPage : ContentPage
{
    private readonly BundleService _bundle;
    private readonly string _sport;

    public ModalitiesPage(BundleService bundle, string sport)
    {
        InitializeComponent();
        _bundle = bundle;
        _sport = sport;
        HeaderLabel.Text = sport;
        Title = sport;
        Populate();
    }

    private void Populate()
    {
        var rows = _bundle.Data!.Conditioning
            .Where(p => p.Sport == _sport)
            .GroupBy(p => p.Modality)
            .Select(g => new ModalityRow(g.Key, g.Count()))
            .OrderBy(r => r.Modality, StringComparer.Ordinal)
            .ToList();
        ModalitiesList.ItemsSource = rows;
    }

    private async void OnModalitySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ModalityRow row)
        {
            return;
        }

        ModalitiesList.SelectedItem = null;
        await Navigation.PushAsync(new ProtocolsPage(_bundle, _sport, row.Modality));
    }
}