using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Memoria.Contracts;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Vault;

/// <summary>
/// Suggests links only: Sentium never writes to the vault, so the user adds any links they agree with in Obsidian.
/// </summary>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Vault, Action = ToolAction.Read)]
internal sealed class SuggestVaultLinksTool(
    IVaultAgentAccess vault,
    IPdpContextAccessor pdpContext,
    ILogger<SuggestVaultLinksTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            SuggestAsync,
            ToolNames.SuggestVaultLinks,
            "Suggest [[wikilinks]] for a note in the user's Obsidian vault: notes with similar content that it is not linked " +
            "with yet. Only suggests - the user adds the links in Obsidian themselves.");
    }

    public async Task<string> SuggestAsync(
        [Description("The note's title, vault path or alias.")] string note,
        [Description("Maximum suggestions (1-20).")] int? topK = null,
        CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return VaultToolMessages.NoUser;
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            return VaultToolMessages.NoNoteGiven;
        }

        try
        {
            var suggestions = await vault.SuggestLinksAsync(userId, note, topK ?? VaultToolMessages.DefaultTopK, ct);
            return suggestions is null
                ? VaultToolMessages.NoteNotFound(note)
                : VaultToolFormatter.FormatRelated(null, $"Unlinked notes similar to \"{VaultToolFormatter.OneLine(note, 120)}\"", suggestions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vault link suggestions failed");
            return VaultToolMessages.Unavailable;
        }
    }
}
