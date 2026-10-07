using Aeon.App.Models;
using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App.Pages;

/// <summary>Protocols for one sport + modality.</summary>
public partial class ProtocolsPage : ContentPage
{
    private readonly BundleService _bundle;
    private readonly string _sport;
    private readonly string _modality;

    public ProtocolsPage(BundleService bundle, string sport, string modality)
    {
        InitializeComponent();
        _bundle = bundle;
        _sport = sport;
        _modality = modality;
        HeaderLabel.Text = $"{sport} · {modality}";
        Title = modality;
        Populate();
    }

    private void Populate()
    {
        ProtocolsList.ItemsSource = _bundle.Data!.Conditioning
            .Where(p => p.Sport == _sport && p.Modality == _modality)
            .OrderBy(p => p.Id)
            .Select(p => new ProtocolRow(p))
            .ToList();
    }

    private async void OnProtocolSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProtocolRow row)
        {
            return;
        }

        ProtocolsList.SelectedItem = null;
        await Navigation.PushAsync(new ProtocolDetailPage(_bundle, row.Protocol));
    }
}