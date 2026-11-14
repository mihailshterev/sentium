using Microsoft.EntityFrameworkCore;
using Sentium.Locus.Common;
using Sentium.Locus.Entities;

namespace Sentium.Locus.Data;

/// <summary>
/// Household inventory store (schema <c>locus</c>). The inventory is shared by every member of the
/// household, so - unlike Praxis - there are no per-user query filters here.
/// </summary>
internal sealed class LocusDbContext(DbContextOptions<LocusDbContext> options) : DbContext(options)
{
    public const string Schema = "locus";

    /// <summary>
    /// Unique index on (parent, case-folded name); its violation is translated into a 409.
    /// </summary>
    public const string SiblingNameIndex = "IX_Locations_ParentLocationId_NormalizedName";

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<AssetAttachment> AssetAttachments => Set<AssetAttachment>();

    public DbSet<KnowledgeSyncEntry> KnowledgeSyncEntries => Set<KnowledgeSyncEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(LocusLimits.LocationNameMax);
            entity.Property(e => e.NormalizedName).IsRequired().HasMaxLength(LocusLimits.LocationNameMax);
            entity.Property(e => e.Description).HasMaxLength(LocusLimits.LocationTextMax);
            entity.Property(e => e.AccessNotes).HasMaxLength(LocusLimits.LocationTextMax);
            entity.Property(e => e.CreatedBy).HasMaxLength(LocusLimits.UserNameMax);
            entity.Property(e => e.UpdatedBy).HasMaxLength(LocusLimits.UserNameMax);

            // NULLS NOT DISTINCT: two roots with the same name collide too.
            entity.HasIndex(e => new { e.ParentLocationId, e.NormalizedName })
                .IsUnique()
                .AreNullsDistinct(false)
                .HasDatabaseName(SiblingNameIndex);

            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        builder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(LocusLimits.AssetNameMax);
            entity.Property(e => e.Category).HasMaxLength(LocusLimits.CategoryMax);
            entity.Property(e => e.PhysicalDescription).HasMaxLength(LocusLimits.DescriptionMax);
            entity.Property(e => e.Instructions).HasMaxLength(LocusLimits.InstructionsMax);
            entity.Property(e => e.Manufacturer).HasMaxLength(LocusLimits.ManufacturerMax);
            entity.Property(e => e.ModelNumber).HasMaxLength(LocusLimits.IdentifierMax);
            entity.Property(e => e.SerialNumber).HasMaxLength(LocusLimits.IdentifierMax);
            entity.Property(e => e.WarrantyInfo).HasMaxLength(LocusLimits.WarrantyMax);
            entity.Property(e => e.AgentInstructions).HasMaxLength(LocusLimits.AgentInstructionsMax);
            entity.Property(e => e.Tags).IsRequired();
            entity.Property(e => e.SearchText).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(LocusLimits.UserNameMax);
            entity.Property(e => e.UpdatedBy).HasMaxLength(LocusLimits.UserNameMax);
            entity.Property(e => e.KnowledgeContentHash).HasMaxLength(64);

            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.DisplayName);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Tags).HasMethod("gin");

            entity.HasOne(e => e.Location)
                .WithMany(l => l.Assets)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BlobName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(LocusLimits.FileNameMax);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.CreatedBy).HasMaxLength(LocusLimits.UserNameMax);

            entity.HasIndex(e => e.AssetId);

            entity.HasOne(e => e.Asset)
                .WithMany(a => a.Attachments)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<KnowledgeSyncEntry>(entity =>
        {
            entity.HasKey(e => e.AssetId);
            entity.Property(e => e.AssetId).ValueGeneratedNever();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.LastError).HasMaxLength(LocusLimits.SyncErrorMax);
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt });
        });
    }
}
