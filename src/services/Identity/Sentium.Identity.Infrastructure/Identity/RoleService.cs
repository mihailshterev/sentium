using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Sentium.Identity.Infrastructure.Identity;

public sealed class RoleService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ILogger<RoleService> logger) : IRoleService
{
    /// <inheritdoc />
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

        // Hierarchy enforcement: only Sovereign may assign Sovereign
        if (roleName == Roles.Sovereign && highestRequesterRole != Roles.Sovereign)
        {
            return (false, "Only a Sovereign may assign the Sovereign role.");
        }

        // Members cannot assign roles at all — only Sovereigns can manage roles
        if (highestRequesterRole != Roles.Sovereign)
        {
            return (false, "Insufficient privileges to assign roles.");
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

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Requester {RequesterId} assigned role {Role} to user {UserId}.", requesterId, roleName, targetUserId);
        }

        return (true, null);
    }

    /// <inheritdoc />
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

        // Only Sovereigns may remove roles
        if (highestRequesterRole != Roles.Sovereign)
        {
            return (false, "Insufficient privileges to remove roles.");
        }

        // Prevent a Sovereign from removing their own Sovereign role if it would leave no Sovereigns
        if (requesterId == targetUserId && roleName == Roles.Sovereign)
        {
            var sovereigns = await userManager.GetUsersInRoleAsync(Roles.Sovereign);
            if (sovereigns.Count <= 1)
            {
                return (false, "Cannot remove the last Sovereign from the system.");
            }
        }

        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return (false, "Target user not found.");
        }

        var result = await userManager.RemoveFromRoleAsync(target, roleName);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return (false, errors);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Requester {RequesterId} removed role {Role} from user {UserId}.", requesterId, roleName, targetUserId);
        }

        return (true, null);
    }

    /// <inheritdoc />
    public async Task<IList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return [];
        }

        return await userManager.GetRolesAsync(user);
    }

    private static string GetHighestRole(IEnumerable<string> roles)
    {
        return roles
            .Where(Roles.IsValid)
            .OrderByDescending(r => ((IList<string>)Roles.Hierarchy).IndexOf(r))
            .FirstOrDefault() ?? string.Empty;
    }
}
