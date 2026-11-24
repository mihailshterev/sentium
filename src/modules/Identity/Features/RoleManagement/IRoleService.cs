namespace Sentium.Identity.Features.RoleManagement;

internal interface IRoleService
{
    /// <summary>
    /// Fails unless <paramref name="requesterId"/> is a <see cref="Security.Roles.Sovereign"/> and
    /// <paramref name="roleName"/> is one of <see cref="Security.Roles"/>.
    /// </summary>
    Task<(bool Succeeded, string? Error)> AssignRoleAsync(Guid requesterId, Guid targetUserId, string roleName, CancellationToken ct);

    /// <summary>
    /// Fails unless <paramref name="requesterId"/> is a <see cref="Security.Roles.Sovereign"/>, and never removes the
    /// last Sovereign so the system stays administrable.
    /// </summary>
    Task<(bool Succeeded, string? Error)> RemoveRoleAsync(Guid requesterId, Guid targetUserId, string roleName, CancellationToken ct);

    ValueTask<IList<string>> GetRolesAsync(Guid userId, CancellationToken ct);
}
