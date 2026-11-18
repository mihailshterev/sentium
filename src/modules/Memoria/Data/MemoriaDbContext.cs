using Microsoft.EntityFrameworkCore;
using Sentium.Core.Common;
using Sentium.Infrastructure.Security;
using Sentium.Memoria.Common;
using Sentium.Memoria.Entities;

namespace Sentium.Memoria.Data;

/// <summary>
/// Vault store (schema <c>memoria</c>). A vault is a personal second brain, so like Tempus a Sovereign does NOT bypass
/// the per-user filter. Only system scopes (migrations, the index worker, the agent facade - which then filters on an
/// explicit user id) see all rows.
/// </summary>
internal sealed class MemoriaDbContext : DbContext
{
    public const string Schema = "memoria";

    public MemoriaDbContext(DbContextOptions<MemoriaDbContext> options, ICurrentUser currentUser) : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        ScopeUserId = currentUser.UserId ?? Guid.Empty;
        IsSystemScope = currentUser.IsSystem;
    }

    public DbSet<Vault> Vaults => Set<Vault>();

    public DbSet<VaultNote> Notes => Set<VaultNote>();

    public DbSet<NoteLink> Links => Set<NoteLink>();

    public DbSet<NoteIndexEntry> IndexEntries => Set<NoteIndexEntry>();

    private Guid ScopeUserId { get; }

    private bool IsSystemScope { get; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampUserOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampUserOwnership();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<Vault>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(MemoriaLimits.VaultNameMax);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<VaultNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(MemoriaLimits.PathMax);
            entity.Property(e => e.PathKey).IsRequired().HasMaxLength(MemoriaLimits.PathMax);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(MemoriaLimits.TitleMax);
            entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(MemoriaLimits.HashLength);
            entity.Property(e => e.IndexedHash).HasMaxLength(MemoriaLimits.HashLength);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Tags).IsRequired();
            entity.Property(e => e.Aliases).IsRequired();
            entity.Property(e => e.AliasKeys).IsRequired();
            entity.Property(e => e.FrontmatterJson).HasColumnType("jsonb");

            entity.HasIndex(e => new { e.VaultId, e.Path }).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.PathKey });
            entity.HasIndex(e => e.Tags).HasMethod("gin");
            entity.HasIndex(e => e.AliasKeys).HasMethod("gin");

            entity.HasOne<Vault>()
                .WithMany()
                .HasForeignKey(e => e.VaultId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<NoteLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Target).IsRequired().HasMaxLength(MemoriaLimits.PathMax);
            entity.Property(e => e.TargetKey).IsRequired().HasMaxLength(MemoriaLimits.PathMax);
            entity.Property(e => e.Heading).HasMaxLength(MemoriaLimits.HeadingMax);

            entity.HasIndex(e => new { e.UserId, e.TargetKey });
            entity.HasIndex(e => e.TargetNoteId);

            entity.HasOne(e => e.SourceNote)
                .WithMany(n => n.OutgoingLinks)
                .HasForeignKey(e => e.SourceNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<VaultNote>()
                .WithMany()
                .HasForeignKey(e => e.TargetNoteId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<NoteIndexEntry>(entity =>
        {
            entity.HasKey(e => e.NoteId);
            entity.Property(e => e.NoteId).ValueGeneratedNever();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.LastError).HasMaxLength(MemoriaLimits.IndexErrorMax);
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt });
            entity.HasIndex(e => e.UserId);
        });
    }

    private void StampUserOwnership()
    {
        if (ScopeUserId == Guid.Empty)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IUserOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.UserId == Guid.Empty)
            {
                entry.Entity.UserId = ScopeUserId;
            }
        }
    }
}
