using IdentityProvider.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityProvider.Infrastructure.Data;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : IdentityDbContext<ApplicationUser,
                                                                                    ApplicationRole, Guid, IdentityUserClaim<Guid>,
                                                                                    IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
                                                                                    IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b => b.ToTable("Users"));
        builder.Entity<ApplicationRole>(b => b.ToTable("Roles"));
        builder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("UserLogins"));
        builder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("RoleClaims"));
        builder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("UserTokens"));

        builder.UseOpenIddict<Guid>();
    }
}
