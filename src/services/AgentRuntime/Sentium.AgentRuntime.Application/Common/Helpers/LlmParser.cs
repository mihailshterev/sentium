using System.Text.Json;
using System.Text.RegularExpressions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Workflows;

namespace Sentium.AgentRuntime.Application.Common.Helpers;

public static partial class LlmParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"\[\s*.*?\s*\]", RegexOptions.Singleline)]
    private static partial Regex JsonArrayRegex();

    [GeneratedRegex(@"RISK:\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex RiskRegex();

    [GeneratedRegex(@"RECOMMENDATION:\s*(.*)", RegexOptions.IgnoreCase)]
    private static partial Regex RecommendationRegex();

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```")]
    private static partial Regex CleanJsonRegex();

    public static List<string> ParseAgentRoles(string llmOutput, Dictionary<string, string> dbAgentMap, IAgentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(llmOutput);
        ArgumentNullException.ThrowIfNull(dbAgentMap);
        ArgumentNullException.ThrowIfNull(registry);

        try
        {
            var cleanJson = CleanJsonRegex().Replace(llmOutput, "$1").Trim();
            var match = JsonArrayRegex().Match(cleanJson);
            if (!match.Success)
            {
                return [];
            }

            var parsed = JsonSerializer.Deserialize<List<string>>(match.Value, JsonOptions);
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

    public static WorkflowResult ParseWorkflowResult(string validatorOutput, List<string> roles, IReadOnlyList<WorkflowLogEntry>? streamLog = null)
    {
        var riskMatch = RiskRegex().Match(validatorOutput);
        var recMatch = RecommendationRegex().Match(validatorOutput);

        return new WorkflowResult
        {
            Explanation = validatorOutput,
            Risk = riskMatch.Groups[1].Value.Trim() is { Length: > 0 } r ? r : "Unknown",
            Recommendation = recMatch.Groups[1].Value.Trim() is { Length: > 0 } rec ? rec : "Review squad logs manually.",
            History = roles.Select(r => ("AgentSelection", r)).ToList(),
            StreamLog = streamLog ?? []
        };
    }
}
