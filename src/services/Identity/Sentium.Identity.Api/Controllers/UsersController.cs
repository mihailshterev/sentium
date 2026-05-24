using System.Security.Claims;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Api.Contracts.Users;
using Sentium.Identity.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Identity.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
public sealed class UsersController(
    IUserManagementService userManagementService,
    IUserClaimsService userClaimsService) : ControllerBase
{
    /// <summary>
    /// Lists all users registered in the system.
    /// </summary>
    /// <remarks>Only accessible by users with the Sovereign role.</remarks>
    /// <response code="200">Returns the full list of users with their basic profiles and roles.</response>
    [HttpGet]
    [Authorize(Roles = Roles.Sovereign)]
    [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers(CancellationToken ct)
    {
        var users = await userManagementService.GetAllUsersAsync(ct);

        var userIds = users.Select(u => u.Id);
        var claimsMap = await userClaimsService.GetBatchClaimsAsync(userIds, [Scopes.Roles], ct);

        var result = users.Select(u =>
        {
            var roleList = new List<string>();
            if (claimsMap.TryGetValue(u.Id, out var claims))
            {
                roleList = claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();
            }

            return new UserResponse(
                u.Id,
                u.Email!,
                u.FirstName,
                u.LastName,
                roleList,
                u.LockoutEnd > DateTimeOffset.UtcNow);
        }).ToList();

        return Ok(result);
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
        var user = await userManagementService.GetUserByIdAsync(id, ct);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userClaimsService.GetClaimsAsync(user.Id, [Scopes.Roles], ct);
        var roleList = roles
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        return Ok(new UserResponse(user.Id, user.Email!, user.FirstName, user.LastName, roleList, user.LockoutEnd > DateTimeOffset.UtcNow));
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

        var (succeeded, errors) = await userManagementService.DeleteUserAsync(requesterId.Value, id, ct);
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
