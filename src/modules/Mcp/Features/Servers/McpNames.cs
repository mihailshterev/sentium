using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Sentium.Mcp.Features.Servers;

/// <summary>
/// Naming rules for MCP servers and the model-facing names of their tools (<c>{server}__{tool}</c>). Names are
/// persisted in agents' tool lists and must survive a paused approval being resumed, so they are deterministic and,
/// once assigned to a tool, never change.
/// </summary>
public static partial class McpNames
{
    /// <summary>
    /// Separator between the server slug and the tool name. Built-in tool names never contain it.
    /// </summary>
    public const string Separator = "__";

    /// <summary>
    /// Function-name limit shared by OpenAI-compatible APIs (<c>^[A-Za-z0-9_-]{1,64}$</c>).
    /// </summary>
    public const int MaxToolNameLength = 64;

    public const int MaxServerNameLength = 24;

    private const int HashSuffixLength = 8;

    public static bool IsValidServerName(string? name) => name is not null && ServerNamePattern().IsMatch(name);

    /// <summary>
    /// Whether <paramref name="toolName"/> has the shape of an MCP tool name (as opposed to a built-in tool).
    /// </summary>
    public static bool IsMcpToolName(string? toolName) => toolName is not null && toolName.Contains(Separator, StringComparison.Ordinal);

    /// <summary>
    /// The qualified name for <paramref name="toolName"/> on <paramref name="serverName"/>, unique among
    /// <paramref name="taken"/> (case-insensitive). Characters outside <c>[A-Za-z0-9_-]</c> become <c>_</c>; a name that
    /// is too long or collides gets a stable hash suffix derived from the original tool name.
    /// </summary>
    public static string Qualify(string serverName, string toolName, IReadOnlySet<string> taken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(taken);

        var candidate = serverName + Separator + Sanitize(toolName);
        if (candidate.Length <= MaxToolNameLength && !taken.Contains(candidate))
        {
            return candidate;
        }

        var suffix = "_" + Hash(toolName);
        var head = candidate.Length > MaxToolNameLength - suffix.Length ? candidate[..(MaxToolNameLength - suffix.Length)] : candidate;
        var hashed = head + suffix;

        // Two tool names whose hashes share the first 32 bits are not realistic; fail loudly rather than silently alias.
        return taken.Contains(hashed)
            ? throw new InvalidOperationException($"Could not derive a unique tool name for '{toolName}' on MCP server '{serverName}'.")
            : hashed;
    }

    private static string Sanitize(string toolName)
    {
        var sb = new StringBuilder(toolName.Length);
        foreach (var c in toolName)
        {
            sb.Append(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        }

        return sb.ToString();
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..HashSuffixLength];

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,23}$", RegexOptions.CultureInvariant)]
    private static partial Regex ServerNamePattern();
}
