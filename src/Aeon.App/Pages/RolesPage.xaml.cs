using Aeon.App.Models;
using Aeon.App.Services;

namespace Aeon.App.Pages;

public partial class RolesPage : ContentPage
{
    private readonly BundleService _bundle;
    private readonly string _sport;

    public RolesPage(BundleService bundle, string sport)
    {
        InitializeComponent();
        _bundle = bundle;
        _sport = sport;
        Title = sport;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (RolesList.ItemsSource is not null || _bundle.Data is null)
        {
            return;
        }

        RolesList.ItemsSource = _bundle.Data.Complexes
            .Where(c => c.Sport == _sport)
            .GroupBy(c => c.Role)
            .Select(g => new RoleRow(g.Key, g.Count()))
            .OrderBy(r => r.Role == "All" ? 0 : 1)
            .ThenBy(r => r.Role, StringComparer.Ordinal)
            .ToList();
    }

    private async void OnRoleSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not RoleRow row)
        {
            return;
        }

        RolesList.SelectedItem = null;
        await Navigation.PushAsync(new CategoriesPage(_bundle, _sport, row.Role));
    }
}