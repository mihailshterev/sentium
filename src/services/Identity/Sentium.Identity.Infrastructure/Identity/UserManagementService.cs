using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Sentium.Identity.Infrastructure.Identity;

public sealed class UserManagementService(
    UserManager<ApplicationUser> userManager,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ApplicationUser>> GetAllUsersAsync(CancellationToken ct)
    {
        return userManager.Users.ToList();
    }

    /// <inheritdoc />
    public async Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken ct)
    {
        return await userManager.FindByIdAsync(userId.ToString());
    }

    /// <inheritdoc />
    public async Task<(bool Succeeded, string[] Errors)> UpdateProfileAsync(Guid userId, string firstName, string? lastName, string email, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, ["User not found."]);
        }

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null && existing.Id != userId)
            {
                return (false, ["Email is already in use."]);
            }

            var emailResult = await userManager.SetEmailAsync(user, email);
            if (!emailResult.Succeeded)
            {
                return (false, emailResult.Errors.Select(e => e.Description).ToArray());
            }

            var usernameResult = await userManager.SetUserNameAsync(user, email);
            if (!usernameResult.Succeeded)
            {
                return (false, usernameResult.Errors.Select(e => e.Description).ToArray());
            }
        }

        user.FirstName = firstName;
        user.LastName = lastName;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = updateResult.Errors.Select(e => e.Description).ToArray();
            logger.LogWarning("Profile update failed for user {UserId}: {Errors}", userId, string.Join(", ", errors));
            return (false, errors);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Profile updated for user {UserId}.", userId);
        }

        return (true, []);
    }

    /// <inheritdoc />
    public async Task<(bool Succeeded, string[] Errors)> DeleteUserAsync(Guid requesterId, Guid targetUserId, CancellationToken ct)
    {
        if (requesterId == targetUserId)
        {
            return (false, ["You cannot delete your own account."]);
        }

        var target = await userManager.FindByIdAsync(targetUserId.ToString());
        if (target is null)
        {
            return (false, ["User not found."]);
        }

        var targetRoles = await userManager.GetRolesAsync(target);
        if (targetRoles.Contains(Roles.Sovereign))
        {
            var sovereigns = await userManager.GetUsersInRoleAsync(Roles.Sovereign);
            if (sovereigns.Count <= 1)
            {
                return (false, ["Cannot delete the last Sovereign user."]);
            }
        }

        var result = await userManager.DeleteAsync(target);
        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray());
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {TargetId} deleted by requester {RequesterId}.", targetUserId, requesterId);
        }

        return (true, []);
    }
}
