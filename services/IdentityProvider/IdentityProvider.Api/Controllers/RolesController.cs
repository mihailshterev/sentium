using IdentityProvider.Api.Contracts.Roles;
using IdentityProvider.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace IdentityProvider.Api.Controllers;

[ApiController]
[Route("roles")]
public sealed class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpPost("assign")]
    public async Task<IActionResult> AssignRole(AssignRoleRequest request, CancellationToken ct)
    {
        await roleService.AssignRoleAsync(request.UserId, request.RoleName, ct);
        return Ok();
    }

    [HttpPost("remove")]
    public async Task<IActionResult> RemoveRole(RemoveRoleRequest request, CancellationToken ct)
    {
        await roleService.RemoveRoleAsync(request.UserId, request.RoleName, ct);
        return Ok();
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetRoles(Guid userId, CancellationToken ct)
    {
        var roles = await roleService.GetRolesAsync(userId, ct);
        return Ok(roles);
    }

    [HttpGet("{userId}/{roleName}")]
    public async Task<IActionResult> HasRole(Guid userId, string roleName, CancellationToken ct)
    {
        var hasRole = await roleService.UserHasRoleAsync(userId, roleName, ct);
        return Ok(hasRole);
    }
}
