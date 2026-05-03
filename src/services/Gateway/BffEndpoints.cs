using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Sentium.ApiGateway;

public static class BffEndpoints
{
    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/login", (string? returnUrl) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/bff/login-complete?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}"
            };
            properties.Items["returnUrl"] = returnUrl ?? "/";

            return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
        }).AllowAnonymous();

        group.MapGet("/login-complete", (string? returnUrl) =>
        {
            var frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
            var target = returnUrl ?? "/";

            if (!target.StartsWith('/') && !target.StartsWith(frontendOrigin, StringComparison.OrdinalIgnoreCase))
            {
                target = "/";
            }

            if (target.StartsWith('/'))
            {
                target = frontendOrigin + target;
            }

            return Results.Redirect(target);
        }).RequireAuthorization("authenticated");

        group.MapGet("/user", async (HttpContext context) =>
        {
            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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
        }).AllowAnonymous();

        group.MapGet("/logout", () =>
        {
            var frontendOrigin = configuration["Frontend:Origin"] ?? "http://localhost:5173";
            return Results.SignOut(new AuthenticationProperties { RedirectUri = $"{frontendOrigin}/login" }, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
        }).RequireAuthorization("authenticated");

        return app;
    }
}
