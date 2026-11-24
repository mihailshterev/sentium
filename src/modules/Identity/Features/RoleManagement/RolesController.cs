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

    [HttpGet("user/{userId:guid}")]
    [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserRoles(Guid userId, CancellationToken ct)
    {
        var roles = await roleService.GetRolesAsync(userId, ct);
        return Ok(roles);
    }

    /// <response code="400">The role is unknown or the user does not exist.</response>
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

    /// <response code="400">The role is unknown, the user does not exist, or it would remove the last Sovereign.</response>
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
