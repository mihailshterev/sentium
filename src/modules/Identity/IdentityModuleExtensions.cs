using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Core.Constants;
using Sentium.Identity.Contracts;
using Sentium.Identity.Data;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Account;
using Sentium.Identity.Features.Avatars;
using Sentium.Identity.Features.Devices;
using Sentium.Identity.Features.RoleManagement;
using Sentium.Identity.Features.Security;
using Sentium.Identity.Features.Users;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Infrastructure.Testing;

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
            options.Password.RequiredLength = 8;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        // The cookie carries the roles; re-reading the user this often makes a role change or a deleted account take
        // effect within a minute instead of the default 30.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Sentium.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = builder.Environment.IsTesting()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
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

        // Added next to the cookie, never as a default: only endpoints that name the Device scheme accept a Device token.
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, DeviceAuthenticationHandler>(
                DeviceAuthenticationDefaults.AuthenticationScheme, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Sovereign, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx => RoleClaims.IsInRole(ctx.User, SecurityRoles.Sovereign)))
            .AddPolicy(Policies.Device, policy =>
                policy.AddAuthenticationSchemes(DeviceAuthenticationDefaults.AuthenticationScheme)
                      .RequireAuthenticatedUser()
                      .RequireClaim(DeviceAuthenticationDefaults.DeviceIdClaimType));

        var authPermitLimit = builder.Configuration.GetValue("Identity:AuthRateLimit:PermitLimit", 10);

        services.AddRateLimiter(options =>
        {
            // One window per client address: someone guessing passwords must not lock everyone else out of signing in.
            // Behind Caddy the address is the client's (forwarded headers).
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }));
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<IUserDirectory, ScopedUserDirectory>();
        services.AddSingleton<IUserRemovalObserver, UserSettingsRemover>();
        services.AddScoped<IAvatarService, AvatarService>();
        services.AddSingleton<IUserRemovalObserver, AvatarRemover>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<DeviceTokenCache>();
        services.AddScoped<IDeviceAuthenticator, DeviceAuthenticator>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddSingleton<IDeviceDirectory, ScopedDeviceDirectory>();

        services.AddSingleton<IdentitySeeder>();

        services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("identity-db", tags: [ModuleName]);

        return builder;
    }

    public static async Task InitializeIdentityModuleAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await app.Services.MigrateModuleDbContextAsync<IdentityDbContext>();

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(scope.ServiceProvider);
    }
}
