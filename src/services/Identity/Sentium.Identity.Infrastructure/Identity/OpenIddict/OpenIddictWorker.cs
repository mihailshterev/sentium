using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Sentium.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OidcConstants = OpenIddict.Abstractions.OpenIddictConstants;

namespace Sentium.Identity.Infrastructure.Identity.OpenIddict;

public sealed class OpenIddictWorker(IServiceProvider serviceProvider, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync("service-worker", cancellationToken) is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "service-worker",
                // TODO: Replace this
                ClientSecret = "dev-secret",
                Permissions =
                {
                    OidcConstants.Permissions.Endpoints.Token,
                    OidcConstants.Permissions.GrantTypes.ClientCredentials,
                    OidcConstants.Permissions.Prefixes.Scope + Scopes.Api
                }
            }, cancellationToken);
        }

        await SeedGatewayBffClientAsync(manager, cancellationToken);
        await SeedRolesAsync(scope.ServiceProvider, cancellationToken);
        await SeedTestUserAsync(scope.ServiceProvider, cancellationToken);
    }

    private async Task SeedGatewayBffClientAsync(IOpenIddictApplicationManager manager, CancellationToken ct)
    {
        const string clientId = "gateway-bff";

        var gatewaySecret = configuration["Identity:GatewayBffSecret"] ?? throw new InvalidOperationException("Gateway BFF secret is not configured.");

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = gatewaySecret,
            ClientType = OidcConstants.ClientTypes.Confidential,
            ConsentType = OidcConstants.ConsentTypes.Implicit,
            Permissions =
            {
                OidcConstants.Permissions.Endpoints.Authorization,
                OidcConstants.Permissions.Endpoints.Token,
                OidcConstants.Permissions.Endpoints.EndSession,
                OidcConstants.Permissions.GrantTypes.AuthorizationCode,
                OidcConstants.Permissions.GrantTypes.RefreshToken,
                OidcConstants.Permissions.ResponseTypes.Code,
                OidcConstants.Permissions.Prefixes.Scope + Scopes.OpenId,
                OidcConstants.Permissions.Prefixes.Scope + Scopes.Profile,
                OidcConstants.Permissions.Prefixes.Scope + Scopes.Email,
                OidcConstants.Permissions.Prefixes.Scope + Scopes.Api,
                OidcConstants.Permissions.Prefixes.Scope + Scopes.Roles,
                OidcConstants.Permissions.Prefixes.Scope + Scopes.OfflineAccess,
            },
            RedirectUris = { new Uri(configuration["Identity:GatewayRedirectUri"] ?? "https://localhost:7282/bff/callback") },
            PostLogoutRedirectUris = { new Uri(configuration["Identity:GatewayPostLogoutUri"] ?? "https://localhost:7282/bff/logged-out") },
            Requirements =
            {
                OidcConstants.Requirements.Features.ProofKeyForCodeExchange
            }
        };

        var existing = await manager.FindByClientIdAsync(clientId, ct);
        if (existing is null)
        {
            await manager.CreateAsync(descriptor, ct);
        }
        else
        {
            await manager.UpdateAsync(existing, descriptor, ct);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedRolesAsync(IServiceProvider services, CancellationToken ct)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        var roleDefs = new[]
        {
            Roles.Sovereign,
            Roles.Member,
            Roles.Guest,
        };

        foreach (var name in roleDefs)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = name });
            }
        }
    }

    private async Task SeedTestUserAsync(IServiceProvider services, CancellationToken ct)
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
            return;
        }

        var user = new ApplicationUser
        {
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

        await userManager.AddToRoleAsync(user, Roles.Member);
    }
}
