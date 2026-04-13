using IdentityProvider.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityProvider.Infrastructure.Identity.OpenIddict;

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
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.Prefixes.Scope + Core.Security.Scopes.Api
                }
            }, cancellationToken);
        }

        await SeedGatewayBffClientAsync(manager, cancellationToken);
    }

    private async Task SeedGatewayBffClientAsync(IOpenIddictApplicationManager manager, CancellationToken ct)
    {
        const string clientId = "gateway-bff";

        var gatewaySecret = configuration["Identity:GatewayBffSecret"] ?? throw new InvalidOperationException("Gateway BFF secret is not configured.");

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = gatewaySecret,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Core.Security.Scopes.OpenId,
                Permissions.Prefixes.Scope + Core.Security.Scopes.Profile,
                Permissions.Prefixes.Scope + Core.Security.Scopes.Email,
                Permissions.Prefixes.Scope + Core.Security.Scopes.Api,
                Permissions.Prefixes.Scope + Core.Security.Scopes.Roles,
                Permissions.Prefixes.Scope + Core.Security.Scopes.OfflineAccess,
            },
            RedirectUris = { new Uri(configuration["Identity:GatewayRedirectUri"] ?? "https://localhost:7282/bff/callback") },
            PostLogoutRedirectUris = { new Uri(configuration["Identity:GatewayPostLogoutUri"] ?? "https://localhost:7282/bff/logged-out") },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
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
}
