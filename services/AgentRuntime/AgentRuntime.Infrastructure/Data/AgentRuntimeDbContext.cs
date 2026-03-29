using AgentRuntime.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentRuntime.Infrastructure.Data;

public sealed class AgentRuntimeDbContext(DbContextOptions<AgentRuntimeDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<Agent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Description)
                .HasMaxLength(1000);
            entity.HasIndex(e => e.Name)
                .IsUnique();
        });
    }
}
