using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Security;
using Sentium.Nuntius.Common;
using Sentium.Nuntius.Entities;

namespace Sentium.Nuntius.Data;

/// <summary>
/// Inbox store (schema <c>nuntius</c>). Like Tempus and unlike Praxis, a Sovereign does NOT bypass the per-user
/// filter: a user reads only their own Inbox. Only system scopes (the publisher, the retention sweep, migrations)
/// see every row, and they name users explicitly.
/// </summary>
internal sealed class NuntiusDbContext : DbContext
{
    public const string Schema = "nuntius";

    public NuntiusDbContext(DbContextOptions<NuntiusDbContext> options, ICurrentUser currentUser) : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        ScopeUserId = currentUser.UserId ?? Guid.Empty;
        IsSystemScope = currentUser.IsSystem;
    }

    public DbSet<Notification> Notifications => Set<Notification>();

    private Guid ScopeUserId { get; }

    private bool IsSystemScope { get; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(NuntiusLimits.CategoryMax);
            entity.Property(e => e.Severity).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(NuntiusLimits.TitleMax);
            entity.Property(e => e.Body).HasMaxLength(NuntiusLimits.BodyMax);
            entity.Property(e => e.Link).HasMaxLength(NuntiusLimits.LinkMax);
            entity.Property(e => e.DedupeKey).HasMaxLength(NuntiusLimits.DedupeKeyMax);

            entity.HasIndex(e => new { e.UserId, e.UpdatedAt });
            entity.HasIndex(e => e.UpdatedAt);

            // At most one unread Notification per user and key: the publisher updates it instead of adding a second,
            // and this index turns a concurrent duplicate insert into a conflict the publisher retries as an update.
            entity.HasIndex(e => new { e.UserId, e.DedupeKey })
                .IsUnique()
                .HasFilter("\"ReadAt\" IS NULL AND \"DedupeKey\" IS NOT NULL");

            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });
    }
}
