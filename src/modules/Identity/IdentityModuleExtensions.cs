using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Sentium.Core.Constants;
using Sentium.Identity.Features.Abstractions;
using Sentium.Identity.Features.Bff;
using Sentium.Identity.Data;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Security;
using Sentium.Identity.Features.Services;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;

namespace Sentium.Identity;

public static class IdentityModuleExtensions
{
    internal const string ModuleName = "identity";

    /// <summary>
    /// Registers ASP.NET Core Identity. Its cookie becomes the host's default authentication scheme,
    /// so every module's <c>[Authorize]</c> authenticates the portal session cookie.
    /// </summary>
    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<IdentityDbContext>(IdentityDbContext.Schema);

        var services = builder.Services;

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

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Sentium.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Sovereign, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx => RoleClaims.IsInRole(ctx.User, SecurityRoles.Sovereign)));

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("auth", policy =>
            {
                policy.PermitLimit = 10;
                policy.Window = TimeSpan.FromMinutes(1);
                policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                policy.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        services.AddScoped<IUserClaimsService, UserClaimsService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();

        services.AddHostedService<IdentitySeeder>();

        services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("identity-db", tags: [ModuleName]);

        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapBffEndpoints();
    }

    public static Task InitializeIdentityModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Services.MigrateModuleDbContextAsync<IdentityDbContext>();
    }
}
