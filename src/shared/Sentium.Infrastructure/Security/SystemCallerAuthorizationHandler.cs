using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentium.Shared.Constants;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// Marker requirement consumed by <see cref="SystemCallerAuthorizationHandler"/>.
/// </summary>
public sealed class SystemCallerRequirement : IAuthorizationRequirement { }

/// <summary>
/// Validates the <c>X-Internal-Token</c> header against the configured
/// <see cref="InternalApiOptions.ApiKey"/>.  Succeeds when:
/// <list type="bullet">
///   <item>The header value matches the configured key, OR</item>
///   <item>No key is configured (development / not-yet-provisioned environments).</item>
/// </list>
/// </summary>
public sealed class SystemCallerAuthorizationHandler(
    IOptionsMonitor<InternalApiOptions> options,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment,
    ILogger<SystemCallerAuthorizationHandler> logger) : AuthorizationHandler<SystemCallerRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SystemCallerRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        var expected = options.CurrentValue.ApiKey;

        if (string.IsNullOrEmpty(expected))
        {
            if (environment.IsDevelopment())
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            logger.LogCritical("Internal API Key is missing or unassigned in a non-development environment! Rejecting request.");
            context.Fail();
            return Task.CompletedTask;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Task.CompletedTask;
        }

        var header = httpContext.Request.Headers[CommonHeaderNames.InternalToken].ToString();
        if (header == expected)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
