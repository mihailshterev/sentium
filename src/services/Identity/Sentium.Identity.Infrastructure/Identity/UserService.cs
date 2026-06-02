using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Application.Users;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Sentium.Identity.Infrastructure.Data;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Identity.Infrastructure.Identity;

public sealed class UserService(
    UserManager<ApplicationUser> userManager,
    IdentityDbContext dbContext,
    HybridCache cache,
    ILogger<UserService> logger) : IUserService
{
    public async ValueTask<(IReadOnlyList<UserDto> Users, int TotalCount)> GetPagedUsersAsync(int page, int pageSize, CancellationToken ct)
    {
        var all = await cache.GetOrCreateAsync<IReadOnlyList<UserDto>>(
            IdentityCacheKeys.AllUsers,
            async token =>
            {
                var users = await userManager.Users.ToListAsync(token);
                return users
                    .Select(u => new UserDto(u.Id, u.Email, u.FirstName, u.LastName, u.LockoutEnd))
                    .ToList();
            },
            tags: [IdentityCacheKeys.UsersTag],
            cancellationToken: ct);

        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (paged, all.Count);
    }

    public ValueTask<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct)
        => cache.GetOrCreateAsync(
            IdentityCacheKeys.UserById(userId),
            async token =>
            {
                var user = await userManager.FindByIdAsync(userId.ToString());
                return user is null ? null : new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.LockoutEnd);
            },
            tags: [IdentityCacheKeys.UsersTag, IdentityCacheKeys.UserTag(userId)],
            cancellationToken: ct);

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

        user.FirstName = firstName.Trim();
        user.LastName = lastName?.Trim();

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var errors = updateResult.Errors.Select(e => e.Description).ToArray();
            logger.LogWarning("Profile update failed for user {UserId}: {Errors}", userId, string.Join(", ", errors));
            return (false, errors);
        }

        await cache.RemoveByTagAsync(IdentityCacheKeys.UserTag(userId), ct);
        await cache.RemoveAsync(IdentityCacheKeys.AllUsers, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Profile updated for user {UserId}.", userId);
        }

        return (true, []);
    }

    public async Task<(bool Succeeded, string[] Errors)> DeleteUserAsync(Guid requesterId, Guid targetUserId, CancellationToken ct)
    {
        if (requesterId == targetUserId)
        {
            return (false, ["You cannot delete your own account."]);
        }

        var (deleteOk, deleteErrors) = await dbContext.Database.ExecuteInTransactionAsync(async () =>
        {
            var target = await userManager.FindByIdAsync(targetUserId.ToString());
            if (target is null)
            {
                return (false, (string[])["User not found."]);
            }

            var targetRoles = await userManager.GetRolesAsync(target);
            if (targetRoles.Contains(Roles.Sovereign))
            {
                var sovereigns = await userManager.GetUsersInRoleAsync(Roles.Sovereign);
                if (sovereigns.Count <= 1)
                {
                    return (false, (string[])["Cannot delete the last Sovereign user."]);
                }
            }

            var result = await userManager.DeleteAsync(target);
            if (!result.Succeeded)
            {
                return (false, result.Errors.Select(e => e.Description).ToArray());
            }

            return (true, (string[])[]);
        }, IsolationLevel.Serializable, ct);

        if (!deleteOk)
        {
            return (deleteOk, deleteErrors);
        }

        await cache.RemoveByTagAsync(IdentityCacheKeys.UserTag(targetUserId), ct);
        await cache.RemoveAsync(IdentityCacheKeys.AllUsers, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {TargetId} deleted by requester {RequesterId}.", targetUserId, requesterId);
        }

        return (true, []);
    }
}
