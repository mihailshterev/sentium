using Microsoft.EntityFrameworkCore;
using Sentium.Praxis.Data;

namespace Sentium.Praxis.Features.Tools.Workspace;

/// <summary>
/// Shared scoping for the workspace tools. Every query filters on the acting user explicitly: workflow runs execute in
/// a system scope that bypasses the DbContext's per-user query filter, so relying on that filter would expose - and let
/// agents write into - other users' workspaces.
/// </summary>
internal static class WorkspaceToolScope
{
    public const string NoUserError = "Error: Workspace access requires a signed-in user, and no user is associated with this run.";

    /// <summary>
    /// Resolves a workspace the user owns by GUID or (case-insensitive) name.
    /// </summary>
    public static async Task<Guid?> ResolveWorkspaceIdAsync(PraxisDbContext db, Guid userId, string workspace, CancellationToken ct)
    {
        var key = workspace.Trim();
        var owned = db.Workspaces.Where(w => w.UserId == userId);

        if (Guid.TryParse(key, out var id))
        {
            return await owned.AnyAsync(w => w.Id == id, ct) ? id : null;
        }

        var lowered = key.ToLowerInvariant();
        return await owned
            .Where(w => w.Name.ToLower() == lowered)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(ct);
    }
}
