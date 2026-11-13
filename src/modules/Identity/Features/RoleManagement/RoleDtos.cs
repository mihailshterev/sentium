namespace Sentium.Identity.Features.RoleManagement;

public sealed record AssignRoleRequest(Guid UserId, string RoleName);

public sealed record RemoveRoleRequest(Guid UserId, string RoleName);

public sealed record RoleResponse(string Name);
