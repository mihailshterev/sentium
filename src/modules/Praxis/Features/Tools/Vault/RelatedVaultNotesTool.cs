using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Memoria.Contracts;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Vault;

[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Vault, Action = ToolAction.Read)]
internal sealed class RelatedVaultNotesTool(
    IVaultAgentAccess vault,
    IPdpContextAccessor pdpContext,
    ILogger<RelatedVaultNotesTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            GetAsync,
            ToolNames.RelatedVaultNotes,
            "Find notes related to one note in the user's Obsidian vault: notes it links to, notes linking to it, notes " +
            "two links away, and notes with similar content. Use it to explore a topic across the vault.");
    }

    public async Task<string> GetAsync(
        [Description("The note's title, vault path or alias.")] string note,
        [Description("Maximum notes per kind of relation (1-20).")] int? topK = null,
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
            var related = await vault.GetRelatedAsync(userId, note, topK ?? VaultToolMessages.DefaultTopK, ct);
            return related is null
                ? VaultToolMessages.NoteNotFound(note)
                : VaultToolFormatter.FormatRelated(null, $"Notes related to \"{VaultToolFormatter.OneLine(note, 120)}\"", related);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Related vault notes lookup failed");
            return VaultToolMessages.Unavailable;
        }
    }
}
