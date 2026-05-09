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
    public DbSet<ProjectFile> ProjectFiles { get; set; }
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }
    public DbSet<AgentLearning> AgentLearnings { get; set; }

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
            entity.Property(e => e.LogJson);
            entity.HasIndex(e => e.StartedAt);
        });

        builder.Entity<ProjectFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.BlobName).IsRequired();
            entity.Property(e => e.Extension).IsRequired().HasMaxLength(32);
            entity.Property(e => e.SizeBytes).IsRequired();
            entity.Property(e => e.ProcessingStatus).IsRequired();
            entity.HasIndex(e => e.WorkspaceId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Files)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        builder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserHarnessPrompt).HasMaxLength(16_000);
            entity.Property(e => e.UpdatedBy).HasMaxLength(512);
        });

        builder.Entity<AgentLearning>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Tags).HasMaxLength(1000);
            entity.HasIndex(e => e.AgentName);
            entity.HasIndex(e => e.CapturedAt);
            entity.HasIndex(e => e.IsIngested);
        });
    }
}
