using System.Security.Claims;
using IdentityProvider.Application.Abstractions;

namespace IdentityProvider.Infrastructure.Identity;

public sealed class UserClaimsService() : IUserClaimsService
{
    public Task<IEnumerable<Claim>> GetClaimsAsync(string userId, IEnumerable<string> scopes) => throw new NotImplementedException();
}

