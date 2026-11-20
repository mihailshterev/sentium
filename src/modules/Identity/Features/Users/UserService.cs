using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Sentium.Identity.Contracts;
using Sentium.Identity.Data;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Devices;
using Sentium.Identity.Features.Security;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Identity.Features.Users;

internal sealed class UserService(
    UserManager<ApplicationUser> userManager,
    IdentityDbContext dbContext,
    IDeviceService deviceService,
    IEnumerable<IUserRemovalObserver> removalObservers,
    HybridCache cache,
    ILogger<UserService> logger) : IUserService
{
    public async ValueTask<(IReadOnlyList<UserDto> Users, int TotalCount)> GetPagedUsersAsync(int page, int pageSize, CancellationToken ct)
    {
        var all = await cache.GetOrCreateAsync<IReadOnlyList<UserDto>>(
            IdentityCacheKeys.AllUsers,
            LoadAllUsersAsync,
            options: IdentityCacheKeys.EntryOptions,
            tags: [IdentityCacheKeys.UsersTag],
            cancellationToken: ct);

        var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (paged, all.Count);
    }

    public ValueTask<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct)
    {
        return cache.GetOrCreateAsync(
            IdentityCacheKeys.UserById(userId),
            token => LoadUserByIdAsync(userId, token),
            options: IdentityCacheKeys.EntryOptions,
            tags: [IdentityCacheKeys.UsersTag, IdentityCacheKeys.UserTag(userId)],
            cancellationToken: ct);
    }

    public async Task<(bool Succeeded, string[] Errors)> UpdateProfileAsync(Guid userId, string firstName, string? lastName, string userName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        userName = userName.Trim();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (false, ["User not found."]);
        }

        if (!string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await userManager.FindByNameAsync(userName);
            if (existing is not null && existing.Id != userId)
            {
                return (false, ["Username is already in use."]);
            }

            var usernameResult = await userManager.SetUserNameAsync(user, userName);
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

        // The user's Devices are deleted by cascade; their ids are needed afterwards to drop what is cached about them.
        var ownedDevices = await deviceService.GetOwnedAsync(targetUserId, ct);

        var (deleteOk, deleteErrors) = await dbContext.Database.ExecuteInTransactionAsync(
            () => DeleteUnlessLastSovereignAsync(targetUserId),
            IsolationLevel.Serializable,
            ct);

        if (!deleteOk)
        {
            return (deleteOk, deleteErrors);
        }

        deviceService.Forget(ownedDevices);

        await cache.RemoveByTagAsync(IdentityCacheKeys.UserTag(targetUserId), ct);
        await cache.RemoveAsync(IdentityCacheKeys.AllUsers, ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {TargetId} deleted by requester {RequesterId}.", targetUserId, requesterId);
        }

        await RemoveUserDataAsync(targetUserId);

        return (true, []);
    }

    /// <summary>
    /// The account is already gone, so the cleanup runs to the end even if the request is aborted, and one module's
    /// failure does not stop the others.
    /// </summary>
    private async Task RemoveUserDataAsync(Guid userId)
    {
        foreach (var observer in removalObservers)
        {
            try
            {
                await observer.OnUserRemovedAsync(userId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Observer} failed to remove the data of deleted user {UserId}.", observer.GetType().Name, userId);
            }
        }
    }

    private async Task<(bool Succeeded, string[] Errors)> DeleteUnlessLastSovereignAsync(Guid targetUserId)
    {
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

        return (true, []);
    }

    private async ValueTask<IReadOnlyList<UserDto>> LoadAllUsersAsync(CancellationToken ct)
    {
        var users = await dbContext.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        var roles = await LoadRolesAsync(null, ct);

        return [.. users.Select(u => ToDto(u, roles))];
    }

    private async ValueTask<UserDto?> LoadUserByIdAsync(Guid userId, CancellationToken ct)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? null : ToDto(user, await LoadRolesAsync(userId, ct));
    }

    /// <summary>
    /// Role names by user, for one user or (<paramref name="userId"/> <see langword="null"/>) everyone, in one query.
    /// </summary>
    private async Task<ILookup<Guid, string>> LoadRolesAsync(Guid? userId, CancellationToken ct)
    {
        var rows = await (
                from userRole in dbContext.UserRoles
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                where userId == null || userRole.UserId == userId
                select new { userRole.UserId, role.Name })
            .ToListAsync(ct);

        return rows.ToLookup(r => r.UserId, r => r.Name!);
    }

    private static UserDto ToDto(ApplicationUser user, ILookup<Guid, string> roles) =>
        new(user.Id, user.UserName, user.FirstName, user.LastName, user.LockoutEnd, [.. roles[user.Id].Order(StringComparer.Ordinal)], user.AvatarUpdatedAt);
}
