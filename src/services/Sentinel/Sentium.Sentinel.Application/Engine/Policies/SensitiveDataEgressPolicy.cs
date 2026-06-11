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
/// credentials (cloud keys, private keys, API tokens, payment card numbers) and denies the action
/// when a match is found, preventing an agent from persisting or exfiltrating sensitive data into
/// long-term memory, the workspace, or an external/sandboxed call.
/// <para/>
/// Regex patterns and the enable toggle are configured in <see cref="PdpOptions"/>. Payment card
/// numbers are detected separately via a Luhn checksum (rather than a broad numeric regex) so that
/// timestamps, ids, and other high-cardinality numerics do not false-positive. Pure reads and
/// searches are skipped to keep false positives low. The matched pattern is logged server-side but
/// never returned to the caller, so the detection rules are not disclosed to the agent.
/// </summary>
public sealed class SensitiveDataEgressPolicy : IPdpPolicy
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Candidate payment-card sequences: 13-19 digits beginning 3-6 (the major card networks),
    /// optionally separated by single spaces or hyphens. Anchoring on the leading network digit
    /// already excludes the most common numeric noise (e.g. 13-digit epoch-millis timestamps start
    /// with 1); each candidate is then Luhn-validated before a deny is issued.
    /// </summary>
    private static readonly Regex CardCandidate = new(@"\b[3-6]\d(?:[ -]?\d){11,17}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant, MatchTimeout);

    private readonly bool _enabled;
    private readonly ILogger<SensitiveDataEgressPolicy> _logger;
    private readonly IReadOnlyList<Regex> _patterns;

    public SensitiveDataEgressPolicy(IOptions<PdpOptions> options, ILogger<SensitiveDataEgressPolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

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

        if (!_enabled || !IsEgressAction(request.Action))
        {
            return Task.FromResult<PolicyDecision?>(null);
        }

        foreach (var content in EnumerateContent(request))
        {
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            try
            {
                if (TryMatchSecretPattern(content, out var matchedPattern))
                {
                    _logger.LogWarning("Sensitive-data egress blocked: payload matched secret pattern '{Pattern}'.", matchedPattern);
                    return Deny("the payload appears to contain a secret or credential");
                }

                if (ContainsCardNumber(content))
                {
                    _logger.LogWarning("Sensitive-data egress blocked: payload contains a Luhn-valid payment card number.");
                    return Deny("the payload appears to contain a payment card number");
                }
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning("Sensitive-data egress scan timed out on a {Length}-char payload; denied for safety.", content.Length);
                return Deny("the sensitive-data scan timed out on the payload");
            }
        }

        return Task.FromResult<PolicyDecision?>(null);
    }

    private bool TryMatchSecretPattern(string content, out string matchedPattern)
    {
        foreach (var pattern in _patterns)
        {
            if (pattern.IsMatch(content))
            {
                matchedPattern = pattern.ToString();
                return true;
            }
        }

        matchedPattern = string.Empty;
        return false;
    }

    private static bool ContainsCardNumber(string content)
    {
        foreach (Match match in CardCandidate.Matches(content))
        {
            if (IsLuhnValid(match.ValueSpan))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates a candidate digit run (ignoring single space/hyphen separators) against the Luhn
    /// checksum and the 13-19 digit length window used by all major card networks.
    /// </summary>
    private static bool IsLuhnValid(ReadOnlySpan<char> candidate)
    {
        var sum = 0;
        var digits = 0;
        var doubleNext = false;

        for (var i = candidate.Length - 1; i >= 0; i--)
        {
            var c = candidate[i];
            if (c is ' ' or '-')
            {
                continue;
            }

            if (c is < '0' or > '9')
            {
                return false;
            }

            var d = c - '0';
            if (doubleNext)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            digits++;
            doubleNext = !doubleNext;
        }

        return digits is >= 13 and <= 19 && sum % 10 == 0;
    }

    private Task<PolicyDecision?> Deny(string detail) =>
        Task.FromResult<PolicyDecision?>(
            PolicyDecision.Deny(
                $"Sensitive-data egress blocked: {detail}. Storing or transmitting secrets through agents is not permitted.",
                Guid.Empty,
                [Name],
                PolicyRiskLevel.High,
                alert: true));

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
