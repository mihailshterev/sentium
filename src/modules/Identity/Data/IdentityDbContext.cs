using Sentium.Identity.Entities;
using Sentium.Identity.Features.Devices;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Sentium.Identity.Data;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : IdentityDbContext<ApplicationUser,
                                                                                    ApplicationRole, Guid, IdentityUserClaim<Guid>,
                                                                                    IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
                                                                                    IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    public const string Schema = "identity";

    private const int Sha256HexLength = 64;

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DevicePairingCode> DevicePairingCodes => Set<DevicePairingCode>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<ApplicationUser>(b => b.ToTable("Users"));
        builder.Entity<ApplicationRole>(b => b.ToTable("Roles"));
        builder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("UserLogins"));
        builder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("RoleClaims"));
        builder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("UserTokens"));

        // Both tables cascade from the user: deleting an account is what kills its Devices' tokens.
        builder.Entity<Device>(b =>
        {
            b.ToTable("Devices");
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).IsRequired().HasMaxLength(DeviceLimits.NameMax);
            b.Property(d => d.TokenHash).IsRequired().HasMaxLength(Sha256HexLength);
            b.Property(d => d.AppVersion).HasMaxLength(DeviceLimits.AppVersionMax);
            b.HasIndex(d => d.TokenHash).IsUnique();
            b.HasIndex(d => d.OwnerUserId);
            b.HasOne(d => d.Owner).WithMany().HasForeignKey(d => d.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DevicePairingCode>(b =>
        {
            b.ToTable("DevicePairingCodes");
            b.HasKey(c => c.Id);
            b.Property(c => c.CodeHash).IsRequired().HasMaxLength(Sha256HexLength);
            b.HasIndex(c => c.CodeHash).IsUnique();
            b.HasIndex(c => c.UserId);
            b.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
