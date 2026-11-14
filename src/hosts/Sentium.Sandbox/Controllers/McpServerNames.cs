namespace Sentium.Sandbox.Controllers;

internal static partial class McpServerNames
{
    public static bool IsValid(string? name) => name is not null && Pattern().IsMatch(name);

    [System.Text.RegularExpressions.GeneratedRegex("^[a-z0-9][a-z0-9-]{1,23}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Pattern();
}
