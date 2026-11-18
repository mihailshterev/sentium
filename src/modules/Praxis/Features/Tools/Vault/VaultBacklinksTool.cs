using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Memoria.Contracts;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Vault;

[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Vault, Action = ToolAction.Read)]
internal sealed class VaultBacklinksTool(
    IVaultAgentAccess vault,
    IPdpContextAccessor pdpContext,
    ILogger<VaultBacklinksTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            GetAsync,
            ToolNames.VaultBacklinks,
            "List a note's backlinks (the notes in the user's Obsidian vault that link to it) and the notes it links to, " +
            "without its content.");
    }

    public async Task<string> GetAsync(
        [Description("The note's title, vault path or alias.")] string note,
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
            var detail = await vault.GetNoteAsync(userId, note, maxChars: 1, ct);
            return detail is null ? VaultToolMessages.NoteNotFound(note) : VaultToolFormatter.FormatLinks(detail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vault backlink lookup failed");
            return VaultToolMessages.Unavailable;
        }
    }
}
