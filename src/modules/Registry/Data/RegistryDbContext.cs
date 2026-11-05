using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sentium.Registry.Entities;
using Sentium.Registry.Features.Settings;

namespace Sentium.Registry.Data;

public sealed class RegistryDbContext(DbContextOptions<RegistryDbContext> options) : DbContext(options)
{
    public const string Schema = "registry";

    private static readonly JsonSerializerOptions SettingsJsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<SystemSettings> SystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UpdatedBy).HasMaxLength(512);
            entity.HasIndex(e => e.UserId).IsUnique().AreNullsDistinct(false);

            entity.Property(e => e.Settings)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, SettingsJsonOptions),
                    v => JsonSerializer.Deserialize<SettingsContainer>(v, SettingsJsonOptions) ?? new SettingsContainer(),
                    new ValueComparer<SettingsContainer>(
                        (c1, c2) => JsonSerializer.Serialize(c1, SettingsJsonOptions) == JsonSerializer.Serialize(c2, SettingsJsonOptions),
                        c => c == null ? 0 : JsonSerializer.Serialize(c, SettingsJsonOptions).GetHashCode(),
                        c => JsonSerializer.Deserialize<SettingsContainer>(JsonSerializer.Serialize(c, SettingsJsonOptions), SettingsJsonOptions)!
                    ))
                .HasColumnType("jsonb");
        });
    }
}
