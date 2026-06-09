using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Application.Engine.Policies;

/// <summary>
/// Sensitive-data egress filter (OWASP LLM02 - Sensitive Information Disclosure).
/// <para/>
/// Inspects the payload of outbound write/execute requests for high-confidence secrets and
/// credentials (cloud keys, private keys, API tokens, card numbers) and denies the action when a
/// match is found, preventing an agent from persisting or exfiltrating sensitive data into
/// long-term memory, the workspace, or an external/sandboxed call.
/// <para/>
/// Patterns and the enable toggle are configured in <see cref="PdpOptions"/>. Pure reads and
/// searches are skipped to keep false positives low.
/// </summary>
public sealed class SensitiveDataEgressPolicy : IPdpPolicy
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private readonly bool _enabled;
    private readonly IReadOnlyList<Regex> _patterns;

    public SensitiveDataEgressPolicy(IOptions<PdpOptions> options, ILogger<SensitiveDataEgressPolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var opts = options.Value;
        _enabled = opts.EgressScanEnabled;

        var compiled = new List<Regex>(opts.SensitivePatterns.Count);
        foreach (var pattern in opts.SensitivePatterns)
        {
            try
            {
                compiled.Add(new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout));
            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex, "Invalid egress secret pattern '{Pattern}' was skipped.", pattern);
            }
        }

        _patterns = compiled;
    }

    public string Name => "SensitiveDataEgress";

    public Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_enabled || _patterns.Count == 0 || !IsEgressAction(request.Action))
        {
            return Task.FromResult<PolicyDecision?>(null);
        }

        foreach (var content in EnumerateContent(request))
        {
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            foreach (var pattern in _patterns)
            {
                try
                {
                    if (pattern.IsMatch(content))
                    {
                        return Task.FromResult<PolicyDecision?>(
                            PolicyDecision.Deny(
                                "Sensitive-data egress blocked: the payload appears to contain a secret or credential " +
                                $"(matched pattern '{pattern}'). Storing or transmitting secrets through agents is not permitted.",
                                Guid.Empty,
                                [Name],
                                PolicyRiskLevel.High,
                                alert: true));
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    return Task.FromResult<PolicyDecision?>(
                        PolicyDecision.Deny(
                            "Sensitive-data egress scan timed out on the payload; denied for safety.",
                            Guid.Empty,
                            [Name],
                            PolicyRiskLevel.High,
                            alert: true));
                }
            }
        }

        return Task.FromResult<PolicyDecision?>(null);
    }

    private static bool IsEgressAction(string? action) =>
        action is not null
        && (action.Equals("write", StringComparison.OrdinalIgnoreCase)
            || action.Equals("execute", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> EnumerateContent(PolicyRequest request)
    {
        yield return request.ResourceId;

        foreach (var value in request.Metadata.Values)
        {
            yield return value;
        }
    }
}
