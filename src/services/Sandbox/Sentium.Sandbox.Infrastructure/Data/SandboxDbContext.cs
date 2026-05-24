using Microsoft.EntityFrameworkCore;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Infrastructure.Data;

public sealed class SandboxDbContext(DbContextOptions<SandboxDbContext> options) : DbContext(options)
{
    public DbSet<ExecutionLogEntry> ExecutionLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<ExecutionLogEntry>(entity =>
        {
            entity.ToTable("ExecutionLogs");
            entity.HasKey(e => e.JobId);

            entity.Property(e => e.AgentId)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.CorrelationId)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Language)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(e => e.Code).IsRequired();
            entity.Property(e => e.Output).IsRequired();
            entity.Property(e => e.Error).IsRequired();

            entity.OwnsMany(e => e.FileContext, owned =>
            {
                owned.ToJson();
            });

            entity.OwnsMany(e => e.Artifacts, owned =>
            {
                owned.ToJson();
                owned.Property(a => a.BlobUri)
                    .HasConversion(
                        v => v.AbsoluteUri,
                        v => new Uri(v));
            });
        });
    }
}
