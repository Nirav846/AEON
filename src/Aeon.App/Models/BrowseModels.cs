using Aeon.Data;

namespace Aeon.App.Models;

/// <summary>Flat, plain display records mirroring the browse steps. No persistence,
/// no behaviour - the source of truth stays the Aeon.Data bundle models.</summary>
public sealed record SportRow(string Name, int ComplexCount);

public sealed record RoleRow(string Role, int ComplexCount);

public sealed record CategoryRow(string Category, int ComplexCount);

public sealed record IdeaRow(Complex Complex)
{
    public bool HasGoals => Complex.Goals is { Count: > 0 };
    public string GoalsPreview => HasGoals ? string.Join(" · ", Complex.Goals!) : string.Empty;
}

public sealed record SearchRow(Complex Complex, string MatchedIn);