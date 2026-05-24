using System.Security.Claims;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Api.Contracts.Roles;
using Sentium.Identity.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.Identity.Api.Controllers;

[ApiController]
[Route("roles")]
[Authorize(Roles = Roles.Sovereign)]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    /// <summary>
    /// Returns all defined system roles and their associated permission sets.
    /// </summary>
    /// <response code="200">Returns the full list of roles and permissions.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public IActionResult GetRoles()
    {
        var roles = Roles.Hierarchy
            .Select(r => new { Name = r, Permissions = Permissions.GetPermissions(r).ToList() })
            .ToList();

        return Ok(roles);
    }

    /// <summary>
    /// Returns the roles currently assigned to a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the list of role names.</response>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(Guid userId, CancellationToken ct)
    {
        var roles = await roleService.GetRolesAsync(userId, ct);
        return Ok(roles);
    }

    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    /// <remarks>
    /// Only users with 'Sovereign' status can call this.
    /// The system enforces a hierarchy check: the requester must have a higher
    /// rank than the role being assigned.
    /// </remarks>
    /// <param name="request">The user ID and the target role name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Role assigned successfully.</response>
    /// <response code="400">If the role is invalid or hierarchy validation fails.</response>
    /// <response code="401">If the requester's identity cannot be verified.</response>
    [HttpPost("assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null)
        {
            return Unauthorized();
        }

        var (succeeded, error) = await roleService.AssignRoleAsync(requesterId.Value, request.UserId, request.RoleName, ct);
        if (!succeeded)
        {
            return BadRequest(new { Error = error });
        }

        return NoContent();
    }

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    /// <remarks>
    /// Only users with 'Sovereign' status can call this.
    /// Hierarchy enforcement applies to removals to prevent privilege escalation or accidental de-ranking.
    /// </remarks>
    /// <response code="204">Role removed successfully.</response>
    /// <response code="400">If the role removal fails due to business rules.</response>
    [HttpPost("remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleRequest request, CancellationToken ct)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId is null)
        {
            return Unauthorized();
        }

        var (succeeded, error) = await roleService.RemoveRoleAsync(requesterId.Value, request.UserId, request.RoleName, ct);
        if (!succeeded)
        {
            return BadRequest(new { Error = error });
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
