namespace Sentium.Identity.Features.RoleManagement;

public sealed record RemoveRoleRequest(Guid UserId, string RoleName);
