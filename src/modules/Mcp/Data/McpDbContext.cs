using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Data;

/// <summary>
/// MCP server registry (schema <c>mcp</c>). Servers are global and Sovereign-managed, so there is no per-user filter.
/// </summary>
internal sealed class McpDbContext(DbContextOptions<McpDbContext> options) : DbContext(options)
{
    public const string Schema = "mcp";

    public DbSet<McpServer> McpServers { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<McpServer>(entity =>
        {
            entity.ToTable("McpServers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(24);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Transport).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Endpoint).HasMaxLength(2048);
            entity.Property(e => e.Image).HasMaxLength(512);
            entity.Property(e => e.Network).HasMaxLength(128);
            entity.Property(e => e.Variables)
                .HasConversion(
                    variables => JsonSerializer.Serialize(variables, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                    new ValueComparer<Dictionary<string, string>>(
                        (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                        d => d.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key, kv.Value)),
                        d => new Dictionary<string, string>(d)))
                .HasColumnType("jsonb");
            entity.HasIndex(e => e.Name).IsUnique();
            entity.OwnsMany(e => e.Tools, owned => owned.ToJson());
        });
    }
}
