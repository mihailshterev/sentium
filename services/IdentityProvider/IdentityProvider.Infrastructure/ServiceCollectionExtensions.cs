using System.Text;
using IdentityProvider.Infrastructure.Data;
using IdentityProvider.Infrastructure.Identity;
using IdentityProvider.Infrastructure.Identity.OpenIddict;
using IdentityProvider.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityProvider.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>((options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Default"));
            options.UseOpenIddict();
        });

        services.AddHostedService<OpenIddictWorker>();

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        var jwtOptions = services.BuildServiceProvider().GetRequiredService<IOptionsSnapshot<JwtOptions>>().Value;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Cookies.ContainsKey("AccessToken"))
                    {
                        context.Token = context.Request.Cookies["AccessToken"];
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorizationBuilder();

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

        services.AddOpenIddict().AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                   .UseDbContext<IdentityDbContext>();
        }).AddServer(options =>
        {
            options.AllowAuthorizationCodeFlow();

            options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

            options.UseAspNetCore()
                   .EnableAuthorizationEndpointPassthrough()
                   .EnableTokenEndpointPassthrough();

            options.SetAuthorizationEndpointUris("/connect/authorize");
            options.SetTokenEndpointUris("/connect/token");

            options.RegisterScopes("openid", "profile", "email", "api");
        });

        return services;
    }

    public static async Task ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }
}
