using IdentityProvider.Core.Entities;
using IdentityProvider.Core.Users;

namespace IdentityProvider.Infrastructure.Mappings;

public static class UserMappingExtensions
{
    public static ApplicationUser ToApplicationUser(this SystemUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new ApplicationUser
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.Email,
            NormalizedEmail = user.Email.ToUpperInvariant(),
            NormalizedUserName = user.Email.ToUpperInvariant()
        };
    }

    public static SystemUser ToDomainUser(this ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new SystemUser(user.Id, user.Email!);
    }
}
