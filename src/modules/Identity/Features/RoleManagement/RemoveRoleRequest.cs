namespace Sentium.Identity.Features.RoleManagement;

internal sealed record RemoveRoleRequest(Guid UserId, string RoleName);
