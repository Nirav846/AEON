using Aeon.App.Models;
using Aeon.App.Services;

namespace Aeon.App.Pages;

public sealed class BrowseOrder
{
    private static readonly string[] CategorySequence =
        ["Multi", "Primer", "Core", "Knee Dom", "Hip Dom", "Push", "Pull"];

    public static IOrderedEnumerable<string> Categories(IEnumerable<string> categories)
        => categories
            .Distinct()
            .OrderBy(c => Array.IndexOf(CategorySequence, c))
            .ThenBy(c => c, StringComparer.Ordinal);

    public static IOrderedEnumerable<string> Roles(IEnumerable<string> roles)
        => roles
            .Distinct()
            .OrderBy(r => r == "All" ? 0 : 1)
            .ThenBy(r => r, StringComparer.Ordinal);
}