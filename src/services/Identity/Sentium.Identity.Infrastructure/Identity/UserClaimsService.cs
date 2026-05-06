using System.Security.Claims;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace Sentium.Identity.Infrastructure.Identity;

public sealed class UserClaimsService(UserManager<ApplicationUser> userManager) : IUserClaimsService
{
    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(Guid userId, IEnumerable<string> scopes, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException($"User '{userId}' not found.");
        return await BuildUserClaimsAsync(user, scopes);
    }

    /// <inheritdoc />
    public async Task<IDictionary<Guid, IReadOnlyCollection<Claim>>> GetBatchClaimsAsync(IEnumerable<Guid> userIds, IEnumerable<string> scopes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var results = new Dictionary<Guid, IReadOnlyCollection<Claim>>();

        foreach (var id in userIds)
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is not null)
            {
                results[id] = await BuildUserClaimsAsync(user, scopes);
            }
        }

        return results;
    }

    private async Task<IReadOnlyCollection<Claim>> BuildUserClaimsAsync(ApplicationUser user, IEnumerable<string> scopes)
    {
        var claims = new List<Claim>
        {
            new(OpenIddictConstants.Claims.Subject, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(OpenIddictConstants.Claims.Email, user.Email));
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
