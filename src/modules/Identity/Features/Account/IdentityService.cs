using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sentium.Core.Constants;
using Sentium.Identity.Data;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Security;
using Sentium.Infrastructure.Extensions;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Identity.Features.Account;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IdentityDbContext dbContext,
    ISettingsService settings,
    ILogger<IdentityService> logger) : IIdentityService
{
    private const long RegistrationLockKey = 0x4964_5265_6769_7374; // "IdRegist"

    private static readonly string[] FirstAccountRoles = [Roles.Member, Roles.Sovereign];
    private static readonly string[] MemberRoles = [Roles.Member];

    public async Task<RegistrationStatusResponse> GetRegistrationStatusAsync(CancellationToken ct)
    {
        var bootstrap = !await userManager.Users.AnyAsync(ct);
        return new RegistrationStatusResponse(bootstrap || await IsRegistrationOpenAsync(ct), bootstrap);
    }

    public async Task<RegistrationOutcome> RegisterUserAsync(RegisterRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outcome = await dbContext.Database.ExecuteInTransactionAsync(() => CreateUserAsync(request, ct), ct: ct);

        if (outcome.User is { } user && outcome.Result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("User {UserId} registered.", user.Id);
            }
        }

        return outcome;
    }

    public async Task<SignInResult> LoginAsync(string userName, string password)
    {
        var result = await signInManager.PasswordSignInAsync(userName.Trim(), password, false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Login attempt for {UserName} rejected: account locked.", userName);
        }
        else if (!result.Succeeded)
        {
            logger.LogInformation("Failed login attempt for {UserName}.", userName);
        }

        return result;
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    public async Task<IdentityResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User not found." });
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return result;
        }

        await signInManager.RefreshSignInAsync(user);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} changed their password.", userId);
        }

        return result;
    }

    public async Task<bool> SignOutOtherSessionsAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        await userManager.UpdateSecurityStampAsync(user);
        await signInManager.RefreshSignInAsync(user);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} signed out their other sessions.", userId);
        }

        return true;
    }

    /// <summary>
    /// The first account of an installation becomes its Sovereign (there is no other way to get one); after that,
    /// accounts are Members and may only register while a Sovereign keeps registration open.
    /// </summary>
    private async Task<RegistrationOutcome> CreateUserAsync(RegisterRequest request, CancellationToken ct)
    {
        dbContext.ChangeTracker.Clear();

        // Registrations take turns, so of two accounts registering at once on a fresh install only one becomes the
        // Sovereign. A lock, not SERIALIZABLE: every sign-up reads the whole user table, so under SERIALIZABLE a burst
        // of them conflicts on every retry until the retries run out.
        await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({RegistrationLockKey})", ct);

        var isFirstAccount = !await userManager.Users.AnyAsync(ct);
        if (!isFirstAccount && !await IsRegistrationOpenAsync(ct))
        {
            return RegistrationOutcome.ClosedToNewAccounts;
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FirstName = string.Empty,
            LastName = null
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new RegistrationOutcome(result, null);
        }

        foreach (var role in isFirstAccount ? FirstAccountRoles : MemberRoles)
        {
            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                // Rolls the account back with the transaction: a user without its roles must not exist.
                throw new InvalidOperationException($"Failed to assign role {role} to the new user: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }

        if (isFirstAccount)
        {
            logger.LogWarning("The first account, {UserName}, was registered and made Sovereign.", user.UserName);
        }

        return new RegistrationOutcome(result, user);
    }

    private async Task<bool> IsRegistrationOpenAsync(CancellationToken ct)
    {
        var access = await settings.GetValueAsync<AccessSettings>(SettingsKeys.Access, ct: ct);
        return access?.RegistrationOpen == true;
    }
}
