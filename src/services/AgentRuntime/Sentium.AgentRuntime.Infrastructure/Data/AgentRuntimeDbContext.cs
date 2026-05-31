using Microsoft.EntityFrameworkCore;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.Infrastructure.Security;

namespace Sentium.AgentRuntime.Infrastructure.Data;

public sealed class AgentRuntimeDbContext : DbContext
{
    private Guid ScopeUserId { get; }
    private bool BypassUserScope { get; }

    public AgentRuntimeDbContext(DbContextOptions<AgentRuntimeDbContext> options, ICurrentUser currentUser) : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        ScopeUserId = currentUser.UserId ?? Guid.Empty;
        BypassUserScope = currentUser.IsSovereign || currentUser.IsSystem;
    }

    public DbSet<Agent> Agents { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowAgent> WorkflowAgents { get; set; }
    public DbSet<WorkflowRun> WorkflowRuns { get; set; }
    public DbSet<ProjectFile> ProjectFiles { get; set; }
    public DbSet<Workspace> Workspaces { get; set; }
    public DbSet<AgentLearning> AgentLearnings { get; set; }
    public DbSet<AgentSkill> AgentSkills { get; set; }

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
            entity.HasIndex(e => new { e.UserId, e.Name })
                .IsUnique();
            entity.HasQueryFilter(e => BypassUserScope || e.UserId == ScopeUserId);
        });

        builder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(255);
            entity.HasIndex(e => new { e.UserId, e.Title })
                .IsUnique();
            entity.Property(e => e.Model)
                .IsRequired()
                .HasMaxLength(255);
            entity.HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => BypassUserScope || e.UserId == ScopeUserId);
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
            entity.HasQueryFilter(e => BypassUserScope || e.Conversation.UserId == ScopeUserId);
        });

        builder.Entity<Workflow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Description)
                .HasMaxLength(4000);
            entity.HasIndex(e => new { e.UserId, e.Name })
                .IsUnique();
            entity.HasQueryFilter(e => BypassUserScope || e.UserId == ScopeUserId);
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
            entity.HasQueryFilter(e => BypassUserScope || e.Workflow.UserId == ScopeUserId);
        });

        builder.Entity<WorkflowRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TriggerType).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TriggerPayload).IsRequired();
            entity.Property(e => e.Explanation).IsRequired();
            entity.Property(e => e.Risk).IsRequired();
            entity.Property(e => e.Recommendation).IsRequired();
            entity.OwnsMany(e => e.Logs, b => b.ToJson("LogJson"));
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.WorkflowId);
            entity.HasOne(e => e.Workflow)
                .WithMany()
                .HasForeignKey(e => e.WorkflowId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(e => BypassUserScope || e.UserId == ScopeUserId);
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
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Files)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
            entity.HasQueryFilter(e => BypassUserScope || e.UserId == ScopeUserId);
        });

        builder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
            entity.HasQueryFilter(e => BypassUserScope || e.UserId == ScopeUserId);
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
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsGlobal);
            entity.HasQueryFilter(e => BypassUserScope || e.IsGlobal || e.UserId == ScopeUserId);
        });

        builder.Entity<AgentSkill>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.Instructions).IsRequired();
            entity.Property(e => e.SkillType).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.HasIndex(e => new { e.UserId, e.Name }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasQueryFilter(e => BypassUserScope || e.UserId == ScopeUserId);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampUserOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampUserOwnership();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampUserOwnership()
    {
        if (ScopeUserId == Guid.Empty)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<IUserOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.UserId == Guid.Empty)
            {
                entry.Entity.UserId = ScopeUserId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AgentLearning>())
        {
            if (entry.State == EntityState.Added && entry.Entity.UserId is null && !BypassUserScope)
            {
                entry.Entity.UserId = ScopeUserId;
            }
        }
    }
}
