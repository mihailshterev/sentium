using System.Security.Claims;
using IdentityProvider.Application.Abstractions;
using IdentityProvider.Core.Entities;
using IdentityProvider.Core.Security;
using Microsoft.AspNetCore.Identity;

namespace IdentityProvider.Infrastructure.Identity;

public sealed class UserClaimsService(UserManager<ApplicationUser> userManager) : IUserClaimsService
{
    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(Guid userId, IEnumerable<string> scopes, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new InvalidOperationException($"User '{userId}' not found.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        var userClaims = await userManager.GetClaimsAsync(user);
        claims.AddRange(userClaims);

        if (scopes.Contains(Scopes.Roles))
        {
            var roles = await userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        return claims;
    }
}

