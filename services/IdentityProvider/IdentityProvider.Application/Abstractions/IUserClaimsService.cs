using System.Security.Claims;

namespace IdentityProvider.Application.Abstractions;

public interface IUserClaimsService
{
    Task<IReadOnlyCollection<Claim>> GetClaimsAsync(Guid userId, IEnumerable<string> scopes, CancellationToken ct);
}
