using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Identity.Features.Security;

namespace Sentium.Identity.Features.RoleManagement;

[ApiController]
[Route($"{IdentityRoutes.Prefix}/roles")]
[Authorize(Roles = Roles.Sovereign)]
internal sealed class RolesController(IRoleService roleService) : ControllerBase
{
    private static readonly IReadOnlyList<RoleResponse> CachedRoles =
        Roles.Hierarchy
            .Select(r => new RoleResponse(r))
            .ToList();

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoleResponse>), StatusCodes.Status200OK)]
    public IActionResult GetRoles() => Ok(CachedRoles);

    /// <summary>
    /// Returns the roles currently assigned to a specific user.
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(Guid userId, CancellationToken ct)
    {
        var roles = await roleService.GetRolesAsync(userId, ct);
        return Ok(roles);
    }

    /// <summary>
    /// Only users with 'Sovereign' status can call this.
    /// The system enforces a hierarchy check: the requester must have a higher
    /// rank than the role being assigned.
    /// </summary>
    /// <param name="request">The user ID and the target role name.</param>
    /// <response code="400">If the role is invalid or hierarchy validation fails.</response>
    /// <response code="401">If the requester's identity cannot be verified.</response>
    [HttpPost("assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requesterId = GetCurrentUserId();
        if (requesterId is null)
        {
            return Unauthorized();
        }

        var (succeeded, error) = await roleService.AssignRoleAsync(requesterId.Value, request.UserId, request.RoleName, ct);
        if (!succeeded)
        {
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        return NoContent();
    }

    /// <summary>
    /// Only users with 'Sovereign' status can call this.
    /// Hierarchy enforcement applies to removals to prevent privilege escalation or accidental de-ranking.
    /// </summary>
    /// <response code="400">If the role removal fails due to business rules.</response>
    [HttpPost("remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveRole([FromBody] RemoveRoleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requesterId = GetCurrentUserId();
        if (requesterId is null)
        {
            return Unauthorized();
        }

        var (succeeded, error) = await roleService.RemoveRoleAsync(requesterId.Value, request.UserId, request.RoleName, ct);
        if (!succeeded)
        {
            return Problem(detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
