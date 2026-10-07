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

// --- Conditioning ---
// Conditioning is a genuinely different shape from Complex: no role, no category,
// no focus, no execution, no equipment. These rows mirror what the schema actually
// has, so the browse axis is Sport -> Modality -> Protocol rather than the Complex
// Sport -> Role -> Category -> Idea.

public sealed record ConditioningSportRow(string Name, int ProtocolCount);

public sealed record ModalityRow(string Modality, int ProtocolCount);

public sealed record ProtocolRow(ConditioningProtocol Protocol)
{
    /// <summary>
    /// Targets are free-form and context-dependent on modality (pace km/h, watts,
    /// seconds, grade, load, belt instruction), so the raw string is shown verbatim
    /// and no unit label is invented. '-' is a deliberate "no target" marker on 8
    /// records, so it renders as prose rather than looking like a data error.
    /// </summary>
    public string TargetSummary
    {
        get
        {
            var m = Clean(Protocol.MaleTarget);
            var f = Clean(Protocol.FemaleTarget);
            if (m is null && f is null) return "No targets set";
            if (m is null) return $"F: {f}";
            if (f is null) return $"M: {m}";
            return $"M: {m}   ·   F: {f}";
        }
    }

    public static string? Clean(string? raw)
        => string.IsNullOrWhiteSpace(raw) || raw.Trim() == "-" ? null : raw.Trim();
}