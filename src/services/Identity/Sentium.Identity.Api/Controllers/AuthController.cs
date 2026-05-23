using System.Security.Claims;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Sentium.Identity.Api.Controllers;

/// <summary>
/// The central OpenID Connect server controller handling authorization requests and token exchanges.
/// </summary>
/// <remarks>
/// This controller implements the OpenID Connect protocol via OpenIddict.
/// Standard protocol endpoints are mapped to custom logic for claim transformation and destination management.
/// </remarks>
[ApiController]
[Route("auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    IUserClaimsService userClaimsService,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Handles the Authorization Request (interactive login).
    /// </summary>
    /// <remarks>
    /// If the user is not logged into the Identity provider, they are challenged via the Application Scheme.
    /// Once authenticated, an identity is created and returned to the client via a SignIn result.
    /// </remarks>
    [HttpGet("~/connect/authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        if (!User.Identity!.IsAuthenticated)
        {
            var loginPath = configuration["Identity:LoginPath"] ?? "/login";
            var authorizeUri = HttpContext.Request.Path + HttpContext.Request.QueryString;
            return Redirect($"{loginPath}?returnUrl={Uri.EscapeDataString(authorizeUri)}");
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var subClaim = new Claim(OpenIddictConstants.Claims.Subject, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        subClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
        identity.AddClaim(subClaim);

        var emailClaim = new Claim(OpenIddictConstants.Claims.Email, User.FindFirstValue(ClaimTypes.Email)!);
        emailClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(emailClaim);

        foreach (var role in User.FindAll(ClaimTypes.Role))
        {
            var roleClaim = new Claim(ClaimTypes.Role, role.Value);
            roleClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(roleClaim);
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Exchanges a grant (code, refresh_token, or client_credentials) for an Access/Identity token.
    /// </summary>
    /// <remarks>
    /// Supports:
    /// - <c>client_credentials</c>
    /// - <c>authorization_code</c>
    /// - <c>refresh_token</c>
    /// </remarks>
    /// <response code="200">Returns a JSON object containing the tokens.</response>
    /// <response code="400">If the grant type is unsupported or the request is malformed.</response>
    /// <response code="403">If the user or client is no longer valid or authorized.</response>
    [HttpPost("~/connect/token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Exchange(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()!;

        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId!, OpenIddictConstants.Destinations.AccessToken);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var subject = result.Principal!.GetClaim(OpenIddictConstants.Claims.Subject);
            if (subject is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var user = await userManager.FindByIdAsync(subject);
            if (user is null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            var scopes = result.Principal.GetScopes();
            var claims = await userClaimsService.GetClaimsAsync(user.Id, scopes, ct);

            var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(scopes);

            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim));
            }

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest("Unsupported grant type.");
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case ClaimTypes.NameIdentifier:
            case ClaimTypes.Email:
            case ClaimTypes.Name:
            case ClaimTypes.Role:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield return OpenIddictConstants.Destinations.IdentityToken;
                break;
            default:
                yield return OpenIddictConstants.Destinations.AccessToken;
                break;
        }
    }
}

