using IdentityProvider.Application.Abstractions;
using IdentityProvider.Core.Entities;
using IdentityProvider.Core.Security;
using IdentityProvider.Infrastructure.Data;
using IdentityProvider.Infrastructure.Identity;
using IdentityProvider.Infrastructure.Identity.OpenIddict;
using Infrastructure.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace IdentityProvider.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>((options) => options.UseSqlServer(configuration.GetConnectionString("identitydb")));
        services.AddSingleton<IEventBus, NatsEventBus>();

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        services.AddQuartz(options =>
        {
            options.UseSimpleTypeLoader();
            options.UseInMemoryStore(); // TODO: Replace with persistent store for production use
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<IdentityDbContext>()
                       .ReplaceDefaultEntities<Guid>();

                options.UseQuartz();
            })
            .AddServer(options =>
            {
                var issuer = configuration["Identity:Issuer"] ?? "https://localhost:5001/";
                options.SetIssuer(new Uri(issuer));
                options.DisableAccessTokenEncryption();

                options.AllowClientCredentialsFlow();

                options.AllowAuthorizationCodeFlow()
                       .RequireProofKeyForCodeExchange();

                options.AllowRefreshTokenFlow();

                options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                       .DisableTransportSecurityRequirement()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough();

                options.SetAuthorizationEndpointUris("/connect/authorize")
                       .SetTokenEndpointUris("/connect/token")
                       .SetEndSessionEndpointUris("/connect/logout");

                options.RegisterScopes(
                    Scopes.OpenId,
                    Scopes.Profile,
                    Scopes.Email,
                    Scopes.Api,
                    Scopes.Roles,
                    Scopes.OfflineAccess
                );
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<IUserClaimsService, UserClaimsService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddHostedService<OpenIddictWorker>();

        return services;
    }

    public static async Task ApplyMigrations(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }
}
