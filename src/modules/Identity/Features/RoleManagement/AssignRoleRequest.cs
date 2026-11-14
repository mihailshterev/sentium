namespace Sentium.Identity.Features.RoleManagement;

internal sealed record AssignRoleRequest(Guid UserId, string RoleName);
