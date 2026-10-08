using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App.Pages;

public partial class ComplexDetailPage : ContentPage
{
    /// <summary>Accent shared with the NRV icon and the list chevrons. Resolved
    /// from the app resource dictionary rather than hard-coded, so the icon and
    /// the running app cannot drift apart.</summary>
    private static Color AccentColor
        => Application.Current?.Resources.TryGetValue("Accent", out var v) == true && v is Color c
            ? c
            : Color.FromArgb("#8B7CF6");

    private readonly Complex _complex;

    public ComplexDetailPage(BundleService bundle, Complex complex)
    {
        InitializeComponent();
        _complex = complex;
        Title = complex.Name;
        Build();
    }

    private void Build()
    {
        var name = new Label
        {
            Text = _complex.Name,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
        };
        Body.Add(name);

        var chips = new Label
        {
            Text = $"{_complex.Sport} · {_complex.Role} · {_complex.Category}",
            FontSize = 13,
            TextColor = Colors.Gray,
        };
        Body.Add(chips);

        AddSection("Focus", _complex.Focus);
        AddSection("Execution", _complex.Execution);
        AddSection("Swap", _complex.Swap);

        if (_complex.Equipment is { Count: > 0 })
        {
            AddSection("Equipment", string.Join(" · ", _complex.Equipment));
        }
        if (_complex.SwapEquipment is { Count: > 0 })
        {
            AddSection("Swap equipment", string.Join(" · ", _complex.SwapEquipment));
        }

        if (_complex.Goals is { Count: > 0 })
        {
            AddSection("Goals", string.Join(Environment.NewLine, _complex.Goals.Select(g => "• " + g)));
        }
        if (!string.IsNullOrWhiteSpace(_complex.Manual?.Cue))
        {
            AddSection("Coach cue", _complex.Manual!.Cue);
        }
        if (!string.IsNullOrWhiteSpace(_complex.Manual?.Why))
        {
            AddSection("Why it exists", _complex.Manual!.Why);
        }

        var footers = new List<string>();
        if (!string.IsNullOrWhiteSpace(_complex.PatternId))
        {
            footers.Add(_complex.PatternId);
        }
        if (_complex.AlsoSuits is { Count: > 0 })
        {
            footers.Add("also suits: " + string.Join(", ", _complex.AlsoSuits));
        }
        if (_complex.AlsoSuitsRoles is { Count: > 0 })
        {
            footers.Add("also suits roles: " + string.Join(", ", _complex.AlsoSuitsRoles));
        }
        if (_complex.Sources is { Count: > 0 })
        {
            footers.Add("sources: " + string.Join(", ", _complex.Sources));
        }
        if (footers.Count > 0)
        {
            Body.Add(new Label
            {
                Text = string.Join(Environment.NewLine, footers),
                FontSize = 12,
                TextColor = Colors.Gray,
            });
        }
    }

    private void AddSection(string header, string body)
    {
        Body.Add(new Label
        {
            Text = header,
            // Section headers sit above the record title's subtitle and below the
            // title itself: uppercase, letter-spaced, accent-coloured, so a long
            // why/cue block reads as its own section rather than running on from
            // the field above it.
            TextColor = AccentColor,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.1,
            Margin = new Thickness(0, 18, 0, 0),
        });
        Body.Add(new Label
        {
            Text = body,
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
        });
    }
}