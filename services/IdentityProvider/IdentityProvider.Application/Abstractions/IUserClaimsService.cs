using System.Security.Claims;

namespace IdentityProvider.Application.Abstractions;

public interface IUserClaimsService
{
    Task<IEnumerable<Claim>> GetClaimsAsync(string userId, IEnumerable<string> scopes);
}
