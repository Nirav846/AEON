using Aeon.App.Models;
using Aeon.App.Services;

namespace Aeon.App.Pages;

public partial class CategoriesPage : ContentPage
{
    private readonly BundleService _bundle;
    private readonly string _sport;
    private readonly string _role;

    public CategoriesPage(BundleService bundle, string sport, string role)
    {
        InitializeComponent();
        _bundle = bundle;
        _sport = sport;
        _role = role;
        Title = $"{sport} · {role}";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (CategoriesList.ItemsSource is not null || _bundle.Data is null)
        {
            return;
        }

        CategoriesList.ItemsSource = _bundle.Data.Complexes
            .Where(c => c.Sport == _sport && c.Role == _role)
            .GroupBy(c => c.Category)
            .Select(g => new CategoryRow(g.Key, g.Count()))
            .OrderBy(r => BrowseOrder.Categories([r.Category]).First())
            .ToList();
    }

    private async void OnCategorySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CategoryRow row)
        {
            return;
        }

        CategoriesList.SelectedItem = null;
        await Navigation.PushAsync(new ComplexesPage(_bundle, _sport, _role, row.Category));
    }
}