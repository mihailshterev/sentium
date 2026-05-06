using Sentium.Locus.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Sentium.Locus.Infrastructure.Data;

public sealed class LocusDbContext(DbContextOptions<LocusDbContext> options) : DbContext(options)
{
    public DbSet<Location> Locations { get; set; }
    public DbSet<Asset> Assets { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Description)
                .HasMaxLength(1000);

            entity.Property(e => e.AccessNotes)
                .HasMaxLength(1000);

            // Self-referencing hierarchy
            entity.HasOne(e => e.ParentLocation)
                .WithMany(e => e.SubLocations)
                .HasForeignKey(e => e.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        builder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Category)
                .HasMaxLength(100);

            entity.Property(e => e.PhysicalDescription)
                .HasMaxLength(1000);

            entity.Property(e => e.Manufacturer)
                .HasMaxLength(255);

            entity.Property(e => e.ModelNumber)
                .HasMaxLength(255);

            entity.Property(e => e.SerialNumber)
                .HasMaxLength(255);

            entity.Property(e => e.WarrantyInfo)
                .HasMaxLength(2000);

            entity.HasOne(e => e.Location)
                .WithMany(l => l.Assets)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
