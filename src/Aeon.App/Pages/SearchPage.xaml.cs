using Aeon.App.Models;
using Aeon.App.Services;

namespace Aeon.App.Pages;

public partial class SearchPage : ContentPage
{
    private readonly BundleService _bundle;
    private List<Aeon.Data.Complex> _all = new();

    public SearchPage(BundleService bundle)
    {
        InitializeComponent();
        _bundle = bundle;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_bundle.Data is null)
        {
            await _bundle.LoadAsync();
        }

        _all = _bundle.Data?.Complexes ?? new List<Aeon.Data.Complex>();
        if (_bundle.Data is null)
        {
            EmptyLabel.Text = $"No data: {_bundle.Error}";
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            ResultsList.ItemsSource = null;
            CountLabel.Text = string.Empty;
            EmptyLabel.Text = "Type to search the database";
            return;
        }

        var term = e.NewTextValue.Trim();
        var results = new List<SearchRow>();
        foreach (var c in _all)
        {
            var matched = MatchField(term, c);
            if (matched is not null)
            {
                results.Add(new SearchRow(c, matched));
            }
        }

        ResultsList.ItemsSource = results.OrderBy(r => r.Complex.Name, StringComparer.Ordinal).ToList();
        CountLabel.Text = $"{results.Count} match{(results.Count == 1 ? "" : "es")} for \u201C{term}\u201D";
        EmptyLabel.Text = "No matches";
    }

    /// <summary>Ordinal case-insensitive contains across the searchable text fields,
    /// reporting WHICH field matched so the coach can see why a result appeared.</summary>
    private static string? MatchField(string term, Aeon.Data.Complex c)
    {
        if (Contains(c.Name)) return "name";
        if (Contains(c.Focus)) return "focus";
        if (Contains(c.Category)) return "category";
        if (c.Goals is not null && c.Goals.Any(Contains)) return "goals";
        if (Contains(c.Execution)) return "execution";
        if (Contains(c.Swap)) return "swap";
        if (c.Equipment.Any(Contains)) return "equipment";
        return null;

        bool Contains(string s) =>
            s.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private async void OnResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SearchRow row)
        {
            return;
        }

        ResultsList.SelectedItem = null;
        await Navigation.PushAsync(new ComplexDetailPage(_bundle, row.Complex));
    }
}