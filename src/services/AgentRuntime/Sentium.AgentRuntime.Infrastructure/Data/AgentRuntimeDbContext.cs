using Sentium.AgentRuntime.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Sentium.AgentRuntime.Infrastructure.Data;

public sealed class AgentRuntimeDbContext(DbContextOptions<AgentRuntimeDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowAgent> WorkflowAgents { get; set; }
    public DbSet<WorkflowRun> WorkflowRuns { get; set; }

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
            entity.Property(e => e.Model)
                .HasMaxLength(255)
                .HasDefaultValue(string.Empty);
            entity.HasIndex(e => e.Name)
                .IsUnique();
        });

        builder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255);
            entity.HasIndex(e => e.Title)
                .IsUnique();
            entity.Property(e => e.Model)
                .IsRequired()
                .HasMaxLength(255);
            entity.HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.Content)
                .IsRequired();
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Workflow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Description)
                .HasMaxLength(4000);
            entity.HasIndex(e => e.Name)
                .IsUnique();
        });

        builder.Entity<WorkflowAgent>(entity =>
        {
            entity.HasKey(e => new { e.WorkflowId, e.AgentId });
            entity.Property(e => e.Order).IsRequired();
            entity.HasOne(e => e.Workflow)
                .WithMany(w => w.WorkflowAgents)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Agent)
                .WithMany()
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WorkflowRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TriggerType).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TriggerPayload).IsRequired();
            entity.Property(e => e.Explanation).IsRequired();
            entity.Property(e => e.Risk).IsRequired();
            entity.Property(e => e.Recommendation).IsRequired();
            entity.HasIndex(e => e.StartedAt);
        });
    }
}
