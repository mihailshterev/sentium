using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;

namespace Sentium.Identity.Infrastructure.Identity;

public sealed class UserClaimsService(
    UserManager<ApplicationUser> userManager,
    HybridCache cache,
    ILogger<UserClaimsService> logger) : IUserClaimsService
{
    private sealed record ClaimDto(string Type, string Value);

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        LocalCacheExpiration = TimeSpan.FromMinutes(3),
        Expiration = TimeSpan.FromMinutes(3)
    };

    public async Task<IReadOnlyCollection<Claim>> GetClaimsAsync(Guid userId, IEnumerable<string> scopes, CancellationToken ct)
    {
        var scopeKey = string.Join(",", scopes.Order());
        var dtos = await cache.GetOrCreateAsync(
            IdentityCacheKeys.ClaimsFor(userId, scopeKey),
            async token =>
            {
                var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException($"User '{userId}' not found.");
                var claims = await BuildUserClaimsAsync(user, scopes);
                return claims.Select(c => new ClaimDto(c.Type, c.Value)).ToArray();
            },
            CacheOptions,
            tags: [IdentityCacheKeys.UserTag(userId)],
            cancellationToken: ct);

        return dtos.Select(dto => new Claim(dto.Type, dto.Value)).ToArray();
    }

    public async Task<IDictionary<Guid, IReadOnlyCollection<Claim>>> GetBatchClaimsAsync(IEnumerable<Guid> userIds, IEnumerable<string> scopes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var scopeList = scopes.ToList();
        var userIdList = userIds.ToList();
        var results = new Dictionary<Guid, IReadOnlyCollection<Claim>>();

        var users = await userManager.Users
            .Where(u => userIdList.Contains(u.Id))
            .ToListAsync(ct);

        foreach (var user in users)
        {
            try
            {
                var scopeKey = string.Join(",", scopeList.Order());
                var dtos = await cache.GetOrCreateAsync(
                    IdentityCacheKeys.ClaimsFor(user.Id, scopeKey),
                    async token =>
                    {
                        var claims = await BuildUserClaimsAsync(user, scopeList);
                        return claims.Select(c => new ClaimDto(c.Type, c.Value)).ToArray();
                    },
                    CacheOptions,
                    tags: [IdentityCacheKeys.UserTag(user.Id)],
                    cancellationToken: ct);

                results[user.Id] = dtos.Select(dto => new Claim(dto.Type, dto.Value)).ToArray();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load claims for user {UserId}; omitting from batch.", user.Id);
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
                claims.Add(new Claim(OpenIddictConstants.Claims.Role, role));
            }
        }

        return claims;
    }
}
