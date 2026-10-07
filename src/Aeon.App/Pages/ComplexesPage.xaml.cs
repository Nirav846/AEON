using Aeon.App.Models;
using Aeon.App.Services;

namespace Aeon.App.Pages;

public partial class ComplexesPage : ContentPage
{
    private readonly BundleService _bundle;
    private readonly string _sport;
    private readonly string _role;
    private readonly string _category;

    public ComplexesPage(BundleService bundle, string sport, string role, string category)
    {
        InitializeComponent();
        _bundle = bundle;
        _sport = sport;
        _role = role;
        _category = category;
        Title = category;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (IdeasList.ItemsSource is not null || _bundle.Data is null)
        {
            return;
        }

        IdeasList.ItemsSource = _bundle.Data.Complexes
            .Where(c => c.Sport == _sport && c.Role == _role && c.Category == _category)
            .Select(c => new IdeaRow(c))
            .OrderBy(r => r.Complex.Name, StringComparer.Ordinal)
            .ToList();
    }

    private async void OnIdeaSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not IdeaRow row)
        {
            return;
        }

        IdeasList.SelectedItem = null;
        await Navigation.PushAsync(new ComplexDetailPage(_bundle, row.Complex));
    }
}