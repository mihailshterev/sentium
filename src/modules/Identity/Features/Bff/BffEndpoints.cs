using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Sentium.Identity.Features.Bff;

/// <summary>
/// Session endpoints the portal's auth store talks to. The session is the ASP.NET Core Identity
/// cookie issued by <c>account/login</c>; no tokens reach the browser.
/// </summary>
public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/bff");

        group.MapGet("/login", (string? returnUrl, IConfiguration configuration) =>
        {
            var frontendOrigin = FrontendOrigin(configuration);
            var returnPath = ToLocalPath(returnUrl, frontendOrigin);

            return Results.Redirect($"{frontendOrigin}/login?returnUrl={Uri.EscapeDataString(returnPath)}");
        }).AllowAnonymous()
          .WithSummary("Sends the browser to the portal login page, keeping a validated local return path.");

        group.MapGet("/user", async (HttpContext context) =>
        {
            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (!result.Succeeded || result.Principal is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                sub = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier),
                email = result.Principal.FindFirstValue(ClaimTypes.Email),
                name = result.Principal.FindFirstValue(ClaimTypes.Name),
                roles = result.Principal.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray()
            });
        }).AllowAnonymous()
          .WithSummary("Returns the current authenticated user's claims, or 401 if no session.");

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Results.NoContent();
        }).RequireAuthorization()
          .WithSummary("Ends the session by clearing the auth cookie.");

        return app;
    }

    /// <summary>
    /// The portal origin, or empty when the portal is served by this host (same origin).
    /// </summary>
    private static string FrontendOrigin(IConfiguration configuration) => (configuration["Frontend:Origin"] ?? string.Empty).TrimEnd('/');

    /// <summary>
    /// Accepts a local path or an absolute URL on the portal origin; anything else falls back to <c>/</c>
    /// so the login redirect can never become an open redirect.
    /// </summary>
    private static string ToLocalPath(string? returnUrl, string frontendOrigin)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (frontendOrigin.Length > 0 && returnUrl.StartsWith(frontendOrigin, StringComparison.OrdinalIgnoreCase))
        {
            returnUrl = returnUrl[frontendOrigin.Length..];
        }

        var isLocalPath = returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);

        return isLocalPath ? returnUrl : "/";
    }
}
