using Microsoft.EntityFrameworkCore;
using Sentium.Registry.Core.Entities;

namespace Sentium.Registry.Infrastructure.Data;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
{
    public DbSet<SystemSettings> SystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UpdatedBy).HasMaxLength(512);

            entity.OwnsOne(e => e.Settings, settings =>
            {
                settings.ToJson();
                settings.OwnsOne(s => s.Harness);
            });
        });
    }
}
