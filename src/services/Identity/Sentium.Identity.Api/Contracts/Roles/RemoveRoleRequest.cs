namespace Sentium.Identity.Api.Contracts.Roles;

public sealed record RemoveRoleRequest(Guid UserId, string RoleName);
