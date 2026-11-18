using Microsoft.Extensions.AI;
using Sentium.Memoria.Contracts;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Tools.Vault;

[AgentToolPolicy(RiskLevel = ToolRiskLevel.Low, Resource = ToolResource.Vault, Action = ToolAction.Read)]
internal sealed class VaultOverviewTool(
    IVaultAgentAccess vault,
    IPdpContextAccessor pdpContext,
    ILogger<VaultOverviewTool> logger) : IAgentTool
{
    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            GetAsync,
            ToolNames.VaultOverview,
            "Summarize the user's Obsidian vault (their second brain): note and link counts, top tags, orphan notes with no " +
            "links, and links to notes that do not exist yet. Use it to review or tidy the vault's structure.");
    }

    public async Task<string> GetAsync(CancellationToken ct = default)
    {
        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return VaultToolMessages.NoUser;
        }

        try
        {
            var overview = await vault.GetOverviewAsync(userId, ct);
            return overview is null ? VaultToolMessages.NoVault : VaultToolFormatter.FormatOverview(overview);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Vault overview failed");
            return VaultToolMessages.Unavailable;
        }
    }
}
