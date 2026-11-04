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

        group.MapGet("/login", (string? returnUrl, HttpContext context) =>
        {
            var returnPath = ToLocalPath(returnUrl, context.Request.Host);

            return Results.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnPath)}");
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
    /// Accepts a local path or an absolute URL on this host's own origin (the portal is same-origin);
    /// anything else falls back to <c>/</c> so the login redirect can never become an open redirect.
    /// </summary>
    private static string ToLocalPath(string? returnUrl, HostString host)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            && absolute.Authority.Equals(host.ToUriComponent(), StringComparison.OrdinalIgnoreCase))
        {
            returnUrl = absolute.PathAndQuery;
        }

        var isLocalPath = returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);

        return isLocalPath ? returnUrl : "/";
    }
}
