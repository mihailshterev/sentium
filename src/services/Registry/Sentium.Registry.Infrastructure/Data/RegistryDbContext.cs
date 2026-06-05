using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sentium.Registry.Core.Entities;
using Sentium.Registry.Core.Settings;

namespace Sentium.Registry.Infrastructure.Data;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions SettingsJsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<SystemSettings> SystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UpdatedBy).HasMaxLength(512);
            entity.HasIndex(e => e.UserId).IsUnique().HasFilter(null);

            entity.Property(e => e.Settings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, SettingsJsonOptions),
                    v => JsonSerializer.Deserialize<SettingsContainer>(v, SettingsJsonOptions) ?? new SettingsContainer(),
                    new ValueComparer<SettingsContainer>(
                        (c1, c2) => JsonSerializer.Serialize(c1, SettingsJsonOptions) == JsonSerializer.Serialize(c2, SettingsJsonOptions),
                        c => c == null ? 0 : JsonSerializer.Serialize(c, SettingsJsonOptions).GetHashCode(),
                        c => JsonSerializer.Deserialize<SettingsContainer>(JsonSerializer.Serialize(c, SettingsJsonOptions), SettingsJsonOptions)!
                    ))
                .HasColumnType("nvarchar(max)");
        });
    }
}
