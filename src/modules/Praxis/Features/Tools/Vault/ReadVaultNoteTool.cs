using System.ComponentModel;
using Microsoft.Extensions.AI;
using Sentium.Memoria.Contracts;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Vault;

[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Vault, Action = ToolAction.Read)]
internal sealed class ReadVaultNoteTool(
    IVaultAgentAccess vault,
    IPdpContextAccessor pdpContext,
    ILogger<ReadVaultNoteTool> logger) : IAgentTool
{
    /// <summary>
    /// Enough for a long note while leaving room in a small model's context.
    /// </summary>
    private const int MaxChars = 12_000;

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ReadAsync,
            ToolNames.ReadVaultNote,
            "Read one note from the user's Obsidian vault in full (markdown), with its tags, the notes it links to and its " +
            "backlinks. Name the note like a wikilink: its title, path or alias.");
    }

    public async Task<string> ReadAsync(
        [Description("The note's title, vault path (e.g. 'Projects/Sentium.md') or alias.")] string note,
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
            var detail = await vault.GetNoteAsync(userId, note, MaxChars, ct);
            return detail is null ? VaultToolMessages.NoteNotFound(note) : VaultToolFormatter.FormatNote(detail);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vault note read failed");
            return VaultToolMessages.Unavailable;
        }
    }
}
