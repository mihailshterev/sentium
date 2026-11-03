using Microsoft.AspNetCore.Identity;
using Sentium.Identity.Entities;

namespace Sentium.Identity.Features.Security;

/// <summary>
/// Seeds the system roles and, when <c>E2E:SeedTestUser</c> is enabled, the e2e test user. Runs in
/// <see cref="StartingAsync"/> so roles exist before Kestrel accepts the first login. The schema
/// itself is created by the module migrations, which the host applies before starting.
/// </summary>
public sealed class IdentitySeeder(IServiceProvider serviceProvider, IConfiguration configuration) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        await SeedRolesAsync(scope.ServiceProvider);
        await SeedTestUserAsync(scope.ServiceProvider);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var name in Roles.Hierarchy)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = name });
            }
        }
    }

    private async Task SeedTestUserAsync(IServiceProvider services)
    {
        var shouldSeed = configuration.GetValue<bool>("E2E:SeedTestUser");
        if (!shouldSeed)
        {
            return;
        }

        var testEmail = configuration["E2E:UserEmail"];
        var testPassword = configuration["E2E:UserPassword"];

        if (string.IsNullOrWhiteSpace(testEmail) || string.IsNullOrWhiteSpace(testPassword))
        {
            throw new InvalidOperationException("E2E seeding is enabled but credentials are missing.");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(testEmail);
        if (existing is not null)
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(existing);
            await userManager.ResetPasswordAsync(existing, resetToken, testPassword);
            await EnsureRolesAsync(userManager, existing);
            return;
        }

        var fixedId = configuration["E2E:UserId"];
        var user = new ApplicationUser
        {
            Id = fixedId is not null ? Guid.Parse(fixedId) : Guid.CreateVersion7(),
            UserName = testEmail,
            Email = testEmail,
            EmailConfirmed = true,
            FirstName = "E2E",
            LastName = "Test",
        };

        var result = await userManager.CreateAsync(user, testPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to seed E2E test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await EnsureRolesAsync(userManager, user);
    }

    private static async Task EnsureRolesAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
    {
        foreach (var role in new[] { Roles.Member, Roles.Sovereign })
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
