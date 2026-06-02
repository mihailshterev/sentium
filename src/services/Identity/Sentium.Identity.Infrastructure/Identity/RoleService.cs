using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Sentium.Identity.Infrastructure.Data;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Identity.Infrastructure.Identity;

public sealed class RoleService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IdentityDbContext dbContext,
    HybridCache cache,
    ILogger<RoleService> logger) : IRoleService
{
    private static readonly Dictionary<string, int> RoleRanks =
        Roles.Hierarchy
            .Select((role, index) => (role, index))
            .ToDictionary(x => x.role, x => x.index);

    public async Task<(bool Succeeded, string? Error)> AssignRoleAsync(Guid requesterId, Guid targetUserId, string roleName, CancellationToken ct)
    {
        if (!Roles.IsValid(roleName))
        {
            return (false, $"'{roleName}' is not a valid role.");
        }

        var requester = await userManager.FindByIdAsync(requesterId.ToString());
        if (requester is null)
        {
            return (false, "Requester not found.");
        }

        var requesterRoles = await userManager.GetRolesAsync(requester);
        var highestRequesterRole = GetHighestRole(requesterRoles);

        if (highestRequesterRole != Roles.Sovereign)
        {
            return (false, roleName == Roles.Sovereign ? "Only a Sovereign may assign the Sovereign role." : "Insufficient privileges to assign roles.");
        }

        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return (false, "Target user not found.");
        }

        if (!await roleManager.RoleExistsAsync(roleName))
        {
            return (false, $"Role '{roleName}' does not exist.");
        }

        var result = await userManager.AddToRoleAsync(target, roleName);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogWarning("Failed to assign role {Role} to user {UserId}: {Errors}", roleName, targetUserId, errors);
            return (false, errors);
        }

        await cache.RemoveByTagAsync(IdentityCacheKeys.UserTag(targetUserId), ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Requester {RequesterId} assigned role {Role} to user {UserId}.", requesterId, roleName, targetUserId);
        }

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> RemoveRoleAsync(Guid requesterId, Guid targetUserId, string roleName, CancellationToken ct)
    {
        if (!Roles.IsValid(roleName))
        {
            return (false, $"'{roleName}' is not a valid role.");
        }

        var requester = await userManager.FindByIdAsync(requesterId.ToString());
        if (requester is null)
        {
            return (false, "Requester not found.");
        }

        var requesterRoles = await userManager.GetRolesAsync(requester);
        var highestRequesterRole = GetHighestRole(requesterRoles);

        if (highestRequesterRole != Roles.Sovereign)
        {
            return (false, "Insufficient privileges to remove roles.");
        }

        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return (false, "Target user not found.");
        }

        var (removeResult, removeError) = await dbContext.Database.ExecuteInTransactionAsync<(bool, string?)>(async () =>
        {
            if (roleName == Roles.Sovereign)
            {
                var sovereigns = await userManager.GetUsersInRoleAsync(Roles.Sovereign);
                if (sovereigns.Count <= 1)
                {
                    return (false, "Cannot remove the last Sovereign from the system.");
                }
            }

            var result = await userManager.RemoveFromRoleAsync(target, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogWarning("Failed to remove role {Role} from user {UserId}: {Errors}", roleName, targetUserId, errors);
                return (false, errors);
            }

            return (true, null);
        }, IsolationLevel.Serializable, ct);

        if (!removeResult)
        {
            return (removeResult, removeError);
        }

        await cache.RemoveByTagAsync(IdentityCacheKeys.UserTag(targetUserId), ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Requester {RequesterId} removed role {Role} from user {UserId}.", requesterId, roleName, targetUserId);
        }

        return (true, null);
    }

    public ValueTask<IList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
        => cache.GetOrCreateAsync(
            IdentityCacheKeys.RolesFor(userId),
            async token =>
            {
                var user = await userManager.FindByIdAsync(userId.ToString());
                if (user is null)
                {
                    return [];
                }
                return await userManager.GetRolesAsync(user);
            },
            tags: [IdentityCacheKeys.UserTag(userId)],
            cancellationToken: ct);

    private static string GetHighestRole(IEnumerable<string> roles)
    {
        return roles
            .Where(Roles.IsValid)
            .OrderByDescending(r => RoleRanks[r])
            .FirstOrDefault() ?? string.Empty;
    }
}
