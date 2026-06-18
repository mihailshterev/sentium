using System.Text.Json;
using System.Text.RegularExpressions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Workflows;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

public static partial class LlmParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"RISK:\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex RiskRegex();

    [GeneratedRegex(@"RECOMMENDATION:\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex RecommendationRegex();

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```")]
    private static partial Regex CleanJsonRegex();

    [GeneratedRegex(@"VERDICT:\s*(APPROVE|REJECT)", RegexOptions.IgnoreCase)]
    private static partial Regex VerdictRegex();

    [GeneratedRegex(@"REASON:\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex ReasonRegex();

    [GeneratedRegex(@"SANITIZED:\s*([\s\S]*)", RegexOptions.IgnoreCase)]
    private static partial Regex SanitizedRegex();

    [GeneratedRegex(@"STATUS:\s*(PASSED|FAILED|PASS|FAIL|APPROVED|REJECTED)", RegexOptions.IgnoreCase)]
    private static partial Regex StatusRegex();

    [GeneratedRegex(@"CRITIQUE:\s*([\s\S]*?)(?:\n\s*[A-Z][A-Z _]{2,}:|$)", RegexOptions.IgnoreCase)]
    private static partial Regex CritiqueRegex();

    [GeneratedRegex(@"RESPONSIBLE[_ ]AGENTS?:\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex ResponsibleAgentsRegex();

    public static List<string> ParseAgentRoles(string llmOutput, Dictionary<string, string> dbAgentMap, IAgentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(llmOutput);
        ArgumentNullException.ThrowIfNull(dbAgentMap);
        ArgumentNullException.ThrowIfNull(registry);

        try
        {
            var arrayJson = ExtractJsonArray(llmOutput);
            if (arrayJson is null)
            {
                return [];
            }

            var parsed = JsonSerializer.Deserialize<List<string>>(arrayJson, JsonOptions);
            if (parsed is null)
            {
                return [];
            }

            var registeredNames = registry.GetRegisteredNames();

            var resolvedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resolved = new List<string>();
            foreach (var p in parsed)
            {
                var dbMatch = dbAgentMap.Keys.FirstOrDefault(k => EqualsIgnoringSpaces(k.AsSpan(), p.AsSpan()));
                if (dbMatch is not null)
                {
                    if (resolvedSet.Add(dbMatch))
                    {
                        resolved.Add(dbMatch);
                    }

                    continue;
                }

                var registryMatch = registeredNames.FirstOrDefault(r => EqualsIgnoringSpaces(r.AsSpan(), p.AsSpan()));
                if (registryMatch is not null && resolvedSet.Add(registryMatch))
                {
                    resolved.Add(registryMatch);
                }
            }

            return resolved;
        }
        catch
        {
            return [];
        }
    }

    public static List<AgentAssignment> ParseAgentAssignments(string llmOutput, Dictionary<string, string> dbAgentMap, IAgentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(llmOutput);
        ArgumentNullException.ThrowIfNull(dbAgentMap);
        ArgumentNullException.ThrowIfNull(registry);

        var arrayJson = ExtractJsonArray(llmOutput);
        if (arrayJson is null)
        {
            return [];
        }

        var registeredNames = registry.GetRegisteredNames().ToList();

        try
        {
            var objs = JsonSerializer.Deserialize<List<AssignmentJson>>(arrayJson, JsonOptions);
            if (objs is not null && objs.Any(o => !string.IsNullOrWhiteSpace(o.Agent)))
            {
                return ResolveAssignments(objs.Select(o => (o.Agent, o.Task ?? string.Empty)), dbAgentMap, registeredNames);
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            var names = JsonSerializer.Deserialize<List<string>>(arrayJson, JsonOptions);
            if (names is not null)
            {
                return ResolveAssignments(names.Select(n => ((string?)n, string.Empty)), dbAgentMap, registeredNames);
            }
        }
        catch (JsonException)
        {
        }

        return [];
    }

    private static List<AgentAssignment> ResolveAssignments(IEnumerable<(string? Agent, string Task)> raw, Dictionary<string, string> dbAgentMap, IReadOnlyList<string> registeredNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AgentAssignment>();

        foreach (var (agent, task) in raw)
        {
            if (string.IsNullOrWhiteSpace(agent))
            {
                continue;
            }

            var canonical = ResolveAgentName(agent, dbAgentMap, registeredNames);
            if (canonical is not null && seen.Add(canonical))
            {
                result.Add(new AgentAssignment(canonical, task?.Trim() ?? string.Empty));
            }
        }

        return result;
    }

    private static string? ResolveAgentName(string candidate, Dictionary<string, string> dbAgentMap, IReadOnlyList<string> registeredNames)
    {
        var dbMatch = dbAgentMap.Keys.FirstOrDefault(k => EqualsIgnoringSpaces(k.AsSpan(), candidate.AsSpan()));
        if (dbMatch is not null)
        {
            return dbMatch;
        }

        return registeredNames.FirstOrDefault(r => EqualsIgnoringSpaces(r.AsSpan(), candidate.AsSpan()));
    }

    private static string? ExtractJsonArray(string llmOutput)
    {
        var cleaned = CleanJsonRegex().Replace(llmOutput, "$1").Trim();
        var start = cleaned.IndexOf('[');
        var end = cleaned.LastIndexOf(']');
        return start >= 0 && end > start ? cleaned[start..(end + 1)] : null;
    }

    private sealed record AssignmentJson(string? Agent, string? Task);

    private static bool EqualsIgnoringSpaces(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        int ia = 0, ib = 0;
        while (ia < a.Length && ib < b.Length)
        {
            while (ia < a.Length && a[ia] == ' ')
            {
                ia++;
            }

            while (ib < b.Length && b[ib] == ' ')
            {
                ib++;
            }

            if (ia >= a.Length || ib >= b.Length)
            {
                break;
            }

            if (char.ToUpperInvariant(a[ia]) != char.ToUpperInvariant(b[ib]))
            {
                return false;
            }

            ia++; ib++;
        }
        while (ia < a.Length && a[ia] == ' ')
        {
            ia++;
        }

        while (ib < b.Length && b[ib] == ' ')
        {
            ib++;
        }

        return ia == a.Length && ib == b.Length;
    }

    public static WorkflowResult ParseWorkflowResult(string validatorOutput, string deliverable, List<string> roles, IReadOnlyList<WorkflowLogEntry>? streamLog = null, Guid? userId = null)
    {
        var riskMatch = RiskRegex().Match(validatorOutput);
        var recMatch = RecommendationRegex().Match(validatorOutput);

        return new WorkflowResult
        {
            Explanation = string.IsNullOrWhiteSpace(deliverable) ? validatorOutput : deliverable,
            Risk = riskMatch.Groups[1].Value.Trim() is { Length: > 0 } r ? r : "Unknown",
            Recommendation = recMatch.Groups[1].Value.Trim() is { Length: > 0 } rec ? rec : "Review squad logs manually.",
            History = roles.Select(r => ("AgentSelection", r)).ToList(),
            StreamLog = streamLog ?? [],
            UserId = userId
        };
    }

    public static ValidationVerdict ParseValidationVerdict(string? validatorOutput)
    {
        if (string.IsNullOrWhiteSpace(validatorOutput))
        {
            return new ValidationVerdict(false, "Validator produced no output.");
        }

        bool passed;
        var statusMatch = StatusRegex().Match(validatorOutput);
        if (statusMatch.Success)
        {
            var token = statusMatch.Groups[1].Value.Trim();
            passed = token.StartsWith("PASS", StringComparison.OrdinalIgnoreCase) || token.StartsWith("APPROV", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var hasPass = validatorOutput.Contains("PASSED", StringComparison.OrdinalIgnoreCase) || validatorOutput.Contains("APPROVED", StringComparison.OrdinalIgnoreCase);
            var hasFail = validatorOutput.Contains("FAILED", StringComparison.OrdinalIgnoreCase) || validatorOutput.Contains("REJECTED", StringComparison.OrdinalIgnoreCase);
            passed = hasPass && !hasFail;
        }

        if (passed)
        {
            return new ValidationVerdict(true, string.Empty);
        }

        var critique = CritiqueRegex().Match(validatorOutput).Groups[1].Value.Trim() is { Length: > 0 } c ? c : validatorOutput.Trim();

        return new ValidationVerdict(false, critique);
    }

    public static List<string> ParseResponsibleAgents(string? validatorOutput, IReadOnlyList<string> squadNames)
    {
        ArgumentNullException.ThrowIfNull(squadNames);

        if (string.IsNullOrWhiteSpace(validatorOutput) || squadNames.Count == 0)
        {
            return [];
        }

        var match = ResponsibleAgentsRegex().Match(validatorOutput);
        if (match.Success)
        {
            var value = match.Groups[1].Value;
            if (value.Contains("None", StringComparison.OrdinalIgnoreCase) && squadNames.All(n => !ContainsAgentName(value, n)))
            {
                return [];
            }

            var fromLine = squadNames.Where(n => ContainsAgentName(value, n)).ToList();
            if (fromLine.Count > 0)
            {
                return fromLine;
            }
        }

        var critique = CritiqueRegex().Match(validatorOutput).Groups[1].Value;
        var haystack = string.IsNullOrWhiteSpace(critique) ? validatorOutput : critique;
        return squadNames.Where(n => ContainsAgentName(haystack, n)).ToList();
    }

    private static bool ContainsAgentName(string haystack, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(haystack))
        {
            return false;
        }

        return Regex.IsMatch(haystack, $@"(?<!\w){Regex.Escape(name.Trim())}(?!\w)", RegexOptions.IgnoreCase);
    }

    public static LearningPromotionVerdict ParseSanitizationVerdict(string llmOutput, string originalContent)
    {
        if (string.IsNullOrWhiteSpace(llmOutput))
        {
            return new LearningPromotionVerdict(false, "Validator returned no output.", originalContent);
        }

        var approved = VerdictRegex().Match(llmOutput) is { Success: true } verdict
            && verdict.Groups[1].Value.Trim().Equals("APPROVE", StringComparison.OrdinalIgnoreCase);

        var reason = ReasonRegex().Match(llmOutput).Groups[1].Value.Trim() is { Length: > 0 } r
            ? r
            : (approved ? "Approved as a generalizable, abstracted pattern." : "Did not meet the abstraction and generalizability criteria.");

        var sanitized = SanitizedRegex().Match(llmOutput).Groups[1].Value.Trim() is { Length: > 0 } s
            ? s
            : originalContent;

        return new LearningPromotionVerdict(approved, reason, sanitized);
    }
}
