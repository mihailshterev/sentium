using Microsoft.EntityFrameworkCore;

namespace Sentium.Sentinel.Infrastructure.Data;

public sealed class SentinelDbContext(DbContextOptions<SentinelDbContext> options) : DbContext(options)
{
    public DbSet<AuditLogEntity> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AgentId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.SkillName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ResourceId).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserPromptHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CorrelationId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Effect).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Risk).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Reason).IsRequired();
            entity.Property(e => e.MetadataJson).IsRequired();
            entity.Property(e => e.TriggeredPoliciesJson).IsRequired();

            entity.HasIndex(e => e.AgentId);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
