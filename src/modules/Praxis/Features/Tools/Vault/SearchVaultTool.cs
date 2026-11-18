using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Memoria.Contracts;
using Sentium.Memoria.Contracts.Models;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Vault;

[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Vault, Action = ToolAction.Search)]
internal sealed class SearchVaultTool(
    IVaultAgentAccess vault,
    IPdpContextAccessor pdpContext,
    ILogger<SearchVaultTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            SearchAsync,
            ToolNames.SearchVault,
            "Search the user's own notes in their Obsidian vault (their second brain) by meaning. Optionally only notes with " +
            "a tag or in a folder. Returns the best matching passage of each note with a link to open it in Obsidian.");
    }

    public async Task<string> SearchAsync(
        [Description("What to look for, in words.")] string query,
        [Description("Optional tag without '#', e.g. 'project' (nested tags like 'project/sentium' match too).")] string? tag = null,
        [Description("Optional folder in the vault, e.g. 'Areas/Health'.")] string? folder = null,
        [Description("Maximum number of notes (1-20).")] int? topK = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return VaultToolMessages.NoUser;
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return "Error: No search query provided.";
        }

        try
        {
            var hits = await vault.SearchAsync(
                userId,
                new VaultSearchQuery(query.Trim(), topK ?? VaultToolMessages.DefaultTopK, Blank(tag), Blank(folder)),
                ct);

            return hits.Count == 0
                ? VaultToolMessages.NoMatches
                : VaultToolFormatter.FormatHits(hits);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vault search failed");
            return VaultToolMessages.Unavailable;
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
