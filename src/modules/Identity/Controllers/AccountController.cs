using System.Security.Claims;
using Sentium.Identity.Features.Abstractions;
using Sentium.Identity.Dtos;
using Sentium.Identity.Features.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Sentium.Identity.Controllers;

/// <summary>
/// Provides endpoints for authentication, session, registration, and personal profile management.
/// </summary>
[ApiController]
[Route($"{IdentityRoutes.Prefix}/account")]
public sealed class AccountController(
    IIdentityService identityService,
    IUserService userService) : ControllerBase
{
    /// <summary>
    /// Registers a new user in the system.
    /// </summary>
    /// <response code="200">Returns the new user's unique identifier and username.</response>
    /// <response code="400">If the registration data is invalid or the username is already in use.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (result, user) = await identityService.RegisterUserAsync(request);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return Ok(new RegisterResponse(user!.Id, user.UserName));
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
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromQuery] string? returnUrl)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await identityService.LoginAsync(request.UserName, request.Password);

        if (result.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, "Account is locked due to too many failed attempts.");
        }

        if (result.RequiresTwoFactor)
        {
            return Ok(new LoginResponse(RequiresTwoFactor: true));
        }

        if (!result.Succeeded)
        {
            return Unauthorized("Invalid login attempt.");
        }

        var redirectTo = (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? returnUrl : "/";
        return Ok(new LoginResponse(RedirectUrl: redirectTo));
    }

    /// <summary>
    /// Ends the current session by clearing the auth cookie.
    /// </summary>
    /// <response code="204">Session ended.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await identityService.LogoutAsync();
        return NoContent();
    }

    /// <summary>
    /// Returns the identity of the current session (subject, username and roles) straight from the auth cookie.
    /// </summary>
    /// <remarks>Cheap by design: it never touches the database, so the portal can use it as its "am I signed in?" check.</remarks>
    /// <response code="200">Returns the session identity.</response>
    /// <response code="401">If there is no valid session.</response>
    [HttpGet("session")]
    [Authorize]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetSession()
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();

        return Ok(new SessionResponse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.FindFirstValue(ClaimTypes.Name),
            roles));
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

        var user = await userService.GetUserByIdAsync(userId.Value, ct);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new ProfileResponse(user.Id, user.UserName, user.FirstName, user.LastName));
    }

    /// <summary>
    /// Updates the authenticated user's own profile information.
    /// </summary>
    /// <response code="204">Profile updated successfully.</response>
    /// <response code="400">If the update data is invalid (e.g., username conflict).</response>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var (succeeded, errors) = await userService.UpdateProfileAsync(userId.Value, request.FirstName, request.LastName, request.UserName, ct);

        if (!succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["profile"] = errors
            }));
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimNames.Subject);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
