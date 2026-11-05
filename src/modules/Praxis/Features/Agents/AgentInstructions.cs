using System.Text;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Harness;
using Sentium.Praxis.Features.Registry;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Composes an agent's complete system instructions - sent as <see cref="ChatOptions.Instructions"/>, i.e. exactly one
/// system message. Order follows the Agent Framework harness convention: harness guidance first, then the
/// agent-specific role, then the tool catalogue. Context providers (skills, learnings) append after this.
/// </summary>
internal static class AgentInstructions
{
    private const int CatalogDescriptionLimit = 160;

    public static string Compose(HarnessSettingsSnapshot harness, string roleInstructions, IReadOnlyCollection<AIFunction> tools)
    {
        ArgumentNullException.ThrowIfNull(harness);

        var sections = new List<string>(4);

        if (harness.IsBuiltInHarnessEnabled)
        {
            sections.Add(UniversalSystemHarness.Policy.Trim());
        }

        if (!string.IsNullOrWhiteSpace(harness.UserHarnessPrompt))
        {
            sections.Add($"### USER-DEFINED BEHAVIOUR\n{harness.UserHarnessPrompt.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(roleInstructions))
        {
            sections.Add(sections.Count > 0 ? $"### AGENT ROLE\n{roleInstructions.Trim()}" : roleInstructions.Trim());
        }

        if (tools.Count > 0)
        {
            sections.Add(BuildToolCatalog(tools));
        }

        return string.Join("\n\n", sections);
    }

    /// <summary>
    /// Lists the exact names of the agent's tools. The schemas are already sent to the model; this short catalogue
    /// exists because smaller models invent tool names far less often when the names also appear in the prompt.
    /// </summary>
    private static string BuildToolCatalog(IEnumerable<AIFunction> tools)
    {
        var sb = new StringBuilder("### AVAILABLE TOOLS");
        foreach (var tool in tools)
        {
            sb.Append("\n- ").Append(tool.Name).Append(": ").Append(Summarize(tool.Description));
        }

        return sb.ToString();
    }

    private static string Summarize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var oneLine = text.ReplaceLineEndings(" ").Trim();
        return oneLine.Length > CatalogDescriptionLimit ? string.Concat(oneLine.AsSpan(0, CatalogDescriptionLimit), "…") : oneLine;
    }
}
