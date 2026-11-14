using Microsoft.EntityFrameworkCore;
using Sentium.Identity.Contracts;
using Sentium.Identity.Data;
using Sentium.Identity.Features.Security;

namespace Sentium.Identity.Features.Users;

/// <summary>
/// Singleton facade for other modules; each call opens its own DI scope for the Identity DbContext.
/// </summary>
internal sealed class ScopedUserDirectory(IServiceScopeFactory scopeFactory) : IUserDirectory
{
    private static readonly string NormalizedSovereign = Roles.Sovereign.ToUpperInvariant();

    public async Task<IReadOnlyList<Guid>> GetSovereignIdsAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        return await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where role.NormalizedName == NormalizedSovereign
            select userRole.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetHouseholdIdsAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        return await db.Users.Select(u => u.Id).ToListAsync(ct);
    }
}
