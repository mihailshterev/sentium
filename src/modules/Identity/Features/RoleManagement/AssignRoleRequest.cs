namespace Sentium.Identity.Features.RoleManagement;

public sealed record AssignRoleRequest(Guid UserId, string RoleName);
