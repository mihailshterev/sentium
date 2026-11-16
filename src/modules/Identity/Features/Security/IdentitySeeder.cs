using Microsoft.AspNetCore.Identity;
using Sentium.Identity.Entities;
using Sentium.Infrastructure.Testing;

namespace Sentium.Identity.Features.Security;

/// <summary>
/// Seeds the system roles and, in the Testing environment with <c>E2E:SeedTestUser</c> enabled, the e2e test user.
/// Runs during module initialization, right after the migrations and before Kestrel accepts the first request: the
/// roles are data every registration and login depends on.
/// </summary>
internal sealed class IdentitySeeder(IConfiguration configuration, IHostEnvironment environment)
{
    public async Task SeedAsync(IServiceProvider services)
    {
        await SeedRolesAsync(services);
        await SeedTestUserAsync(services);
    }

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
        // Only ever in the Testing environment: the e2e user is a Sovereign with a password from configuration.
        var shouldSeed = environment.IsTesting() && configuration.GetValue<bool>("E2E:SeedTestUser");
        if (!shouldSeed)
        {
            return;
        }

        var testUserName = configuration["E2E:UserName"];
        var testPassword = configuration["E2E:UserPassword"];

        if (string.IsNullOrWhiteSpace(testUserName) || string.IsNullOrWhiteSpace(testPassword))
        {
            throw new InvalidOperationException("E2E seeding is enabled but credentials are missing.");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByNameAsync(testUserName);
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
            UserName = testUserName,
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
