namespace Sentium.Identity.Api.Contracts.Roles;

public sealed record AssignRoleRequest(Guid UserId, string RoleName);
