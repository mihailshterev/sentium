using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Application.Engine.Policies;

/// <summary>
/// Deterministic guard for protected resources and destructive actions.
/// <para/>
/// Enforces the two static allow/deny lists configured in <see cref="PdpOptions"/>:
/// <list type="bullet">
/// <item><b>Protected paths</b> - denies any access whose resource id touches a protected
/// prefix (e.g. <c>.env</c>, <c>appsettings</c>, <c>secret</c>, <c>private_key</c>, <c>/etc/</c>),
/// preventing agents from reading or writing sensitive configuration and credentials. Path
/// separators are normalized first, so a backslash/forward-slash variant of the same path
/// (e.g. <c>C:\Windows\</c> vs <c>C:/Windows/</c>) cannot slip past a configured prefix.</item>
/// <item><b>Forbidden actions</b> - denies requests whose action, skill, or resource id contains a
/// destructive verb (e.g. <c>delete</c>, <c>drop</c>, <c>truncate</c>, <c>escalate</c>) as a whole
/// token. Token boundaries (any non-alphanumeric character, including <c>_</c> and <c>-</c>) avoid
/// false positives on benign substrings such as <c>drop</c> inside <c>dropdown</c>.</item>
/// </list>
/// Matching for protected paths intentionally stays substring-based: reading anything that merely
/// looks like a secret is treated as a leak, so this list errs aggressively toward denial.
/// </summary>
public sealed class ProtectedResourcePolicy : IPdpPolicy
{
    private readonly (string Original, string Normalized)[] _protectedPrefixes;
    private readonly Regex? _forbiddenVerbs;

    public ProtectedResourcePolicy(IOptions<PdpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pdp = options.Value;

        _protectedPrefixes = pdp.ProtectedResourcePrefixes
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => (Original: p, Normalized: NormalizeSeparators(p)))
            .ToArray();

        var verbs = pdp.ForbiddenActions.Where(v => !string.IsNullOrEmpty(v)).ToArray();

        _forbiddenVerbs = verbs.Length == 0
            ? null
            : new Regex(
                $"(?<![A-Za-z0-9])(?:{string.Join('|', verbs.Select(Regex.Escape))})(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    public string Name => "ProtectedResource";

    public Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resourceId = request.ResourceId ?? string.Empty;

        if (_protectedPrefixes.Length > 0)
        {
            var normalizedResource = NormalizeSeparators(resourceId);
            foreach (var (original, normalized) in _protectedPrefixes)
            {
                if (normalizedResource.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return Deny(
                        $"Access to protected resource '{Truncate(resourceId)}' is denied: it matches the " +
                        $"protected pattern '{original}'. Configuration, secrets, and system paths are off-limits to agents.");
                }
            }
        }

        if (_forbiddenVerbs is not null && (TryMatchVerb(request.Action, out var verb) || TryMatchVerb(request.SkillName, out verb) || TryMatchVerb(resourceId, out verb)))
        {
            return Deny(
                $"Forbidden operation '{verb}' detected (skill='{request.SkillName}', action='{request.Action}'). " +
                "Destructive operations are blocked by policy.");
        }

        return Task.FromResult<PolicyDecision?>(null);
    }

    private bool TryMatchVerb(string? value, out string verb)
    {
        verb = string.Empty;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var match = _forbiddenVerbs!.Match(value);
        if (!match.Success)
        {
            return false;
        }

        verb = match.Value;
        return true;
    }

    private Task<PolicyDecision?> Deny(string reason) =>
        Task.FromResult<PolicyDecision?>(
            PolicyDecision.Deny(reason, Guid.Empty, [Name], PolicyRiskLevel.High));

    private static string NormalizeSeparators(string value) => value.Replace('\\', '/');

    private static string Truncate(string value) => value.Length > 120 ? value[..120] : value;
}
