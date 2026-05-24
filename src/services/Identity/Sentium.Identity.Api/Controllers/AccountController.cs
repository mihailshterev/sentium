using System.Security.Claims;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Api.Contracts.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace Sentium.Identity.Api.Controllers;

/// <summary>
/// Provides endpoints for authentication, registration, and personal profile management.
/// </summary>
[ApiController]
[Route("account")]
public sealed class AccountController(
    IIdentityService identityService,
    IUserManagementService userManagementService) : ControllerBase
{
    /// <summary>
    /// Registers a new user in the system.
    /// </summary>
    /// <response code="200">Returns the new user's unique identifier and email.</response>
    /// <response code="400">If the registration data is invalid or the email is already in use.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

    /// <summary>
    /// Authenticates a user and initiates a session.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="returnUrl">Optional URL to redirect to after successful login.</param>
    /// <response code="200">Login successful. Returns redirect info or 2FA requirement.</response>
    /// <response code="401">Invalid credentials provided.</response>
    /// <response code="423">Account is locked due to repeated failed attempts.</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
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

        var redirectTo = (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : "/";
        return Ok(new { RedirectUrl = redirectTo });
    }

    /// <summary>
    /// Logs the user out of the current application session and the identity server.
    /// </summary>
    [HttpGet("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// </summary>
    /// <response code="200">Returns the profile data.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await userManagementService.GetUserByIdAsync(userId.Value, ct);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
        });
    }

    /// <summary>
    /// Updates the authenticated user's own profile information.
    /// </summary>
    /// <response code="204">Profile updated successfully.</response>
    /// <response code="400">If the update data is invalid (e.g., email conflict).</response>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var (succeeded, errors) = await userManagementService.UpdateProfileAsync(userId.Value, request.FirstName, request.LastName, request.Email, ct);

        if (!succeeded)
        {
            return BadRequest(new { Errors = errors });
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
