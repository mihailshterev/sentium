using IdentityProvider.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace IdentityProvider.Api.Controllers;

[ApiController]
[Route("account")]
public sealed class AccountController(IIdentityService identityService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (result, user) = await identityService.RegisterUserAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return Ok(new
        {
            user!.Id,
            user.Email
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromQuery] string? returnUrl)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await identityService.LoginAsync(request.Email, request.Password);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, "Account is locked due to too many failed attempts.");
        }

        if (result.RequiresTwoFactor)
        {
            return Ok(new { RequiresTwoFactor = true });
        }

        if (!result.Succeeded)
        {
            return Unauthorized("Invalid login attempt.");
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Ok(new { RedirectUrl = returnUrl });
        }

        return Ok();
    }

    [HttpGet("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
