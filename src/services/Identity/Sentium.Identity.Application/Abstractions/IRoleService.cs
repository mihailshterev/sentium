namespace Sentium.Identity.Application.Abstractions;

public interface IRoleService
{
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken ct);
    Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken ct);
    Task<IList<string>> GetRolesAsync(Guid userId, CancellationToken ct);
}
