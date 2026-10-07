using Aeon.App.Models;
using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App.Pages;

public partial class ProtocolDetailPage : ContentPage
{
    private readonly ConditioningProtocol _protocol;

    public ProtocolDetailPage(BundleService bundle, ConditioningProtocol protocol)
    {
        InitializeComponent();
        _protocol = protocol;
        Title = protocol.Name;
        Build();
    }

    private void Build()
    {
        Body.Add(new Label
        {
            Text = _protocol.Name,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
        });

        Body.Add(new Label
        {
            Text = $"{_protocol.Sport} · {_protocol.Modality}",
            FontSize = 13,
            TextColor = Colors.Gray,
        });

        AddSection("Work : rest", _protocol.WorkToRest);

        // Targets are printed as raw strings with no unit label. The unit depends on
        // the modality - pace km/h, watts, seconds, grade, load or a belt
        // instruction - so a fixed label would be wrong for most of them.
        var male = ProtocolRow.Clean(_protocol.MaleTarget);
        var female = ProtocolRow.Clean(_protocol.FemaleTarget);

        Body.Add(Header("Male target"));
        Body.Add(Value(male));
        Body.Add(Header("Female target"));
        Body.Add(Value(female));
    }

    private void AddSection(string header, string body)
    {
        Body.Add(Header(header));
        Body.Add(new Label
        {
            Text = body,
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
        });
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.Gray,
    };

    /// <summary>
    /// A missing target and an explicit "-" marker both mean "no target set", and
    /// are shown as prose. Printing a bare hyphen would read to a coach like a data
    /// error rather than a deliberate marker.
    /// </summary>
    private static Label Value(string? raw) => new()
    {
        Text = raw is null ? "No target set" : raw,
        FontSize = 15,
        LineBreakMode = LineBreakMode.WordWrap,
        TextColor = raw is null ? Colors.Gray : Colors.Black,
    };
}