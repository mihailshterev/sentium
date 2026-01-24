namespace IdentityProvider.Api.Contracts.Roles;

public sealed record AssignRoleRequest(Guid UserId, string RoleName);
