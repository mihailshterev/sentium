using System.Security.Claims;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Dtos;
using Sentium.Identity.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace Sentium.Identity.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public sealed class UsersController(IUserService userService, IUserClaimsService userClaimsService) : ControllerBase
{
    /// <summary>
    /// Lists users registered in the system, with pagination.
    /// </summary>
    /// <remarks>Only accessible by users with the Sovereign role.</remarks>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <response code="200">Returns a paginated list of users with their basic profiles and roles.</response>
    [HttpGet]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (users, totalCount) = await userService.GetPagedUsersAsync(page, pageSize, ct);

        var userIds = users.Select(u => u.Id);
        var claimsMap = await userClaimsService.GetBatchClaimsAsync(userIds, [Scopes.Roles], ct);

        var items = users.Select(u =>
        {
            List<string> roleList = [];
            if (claimsMap.TryGetValue(u.Id, out var claims))
            {
                roleList = [.. claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)];
            }

            return new UserResponse(
                u.Id,
                u.Email!,
                u.FirstName,
                u.LastName,
                roleList,
                u.IsLockedOut);
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return Ok(new PagedResponse<UserResponse>(items, totalCount, page, pageSize, totalPages));
    }

    /// <summary>
    /// Retrieves detailed information for a specific user.
    /// </summary>
    /// <param name="id">The unique identifier of the target user.</param>
    /// <response code="200">Returns the user profile.</response>
    /// <response code="404">If the user does not exist.</response>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    {
        var user = await userService.GetUserByIdAsync(id, ct);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userClaimsService.GetClaimsAsync(user.Id, [Scopes.Roles], ct);
        var roleList = roles
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return Ok(new UserResponse(user.Id, user.Email!, user.FirstName, user.LastName, roleList, user.IsLockedOut));
    }

    /// <summary>
    /// Permanently deletes a user from the system.
    /// </summary>
    /// <remarks>
    /// Only accessible by Sovereign.
    /// This operation is destructive and includes a hierarchy check to prevent self-deletion or unauthorized escalation.
    /// </remarks>
    /// <response code="204">User deleted successfully.</response>
    /// <response code="400">If the deletion violates business rules (e.g., deleting yourself).</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null)
        {
            return Unauthorized();
        }

        var (succeeded, errors) = await userService.DeleteUserAsync(requesterId.Value, id, ct);
        if (!succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["user"] = errors
            }));
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(OpenIddictConstants.Claims.Subject);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
