using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentium.Core.Common;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Entities;

namespace Sentium.Tempus.Data;

/// <summary>
/// Year-timeline store (schema <c>tempus</c>). Everything is private to its owner. Unlike Praxis, a Sovereign does
/// NOT bypass the per-user filter: a timeline is a personal planner, and an administrator's own timeline must never
/// fill up with every member's tasks. Only system scopes (migrations, seeders, the agent facade - which then filters
/// on an explicit user id) see all rows.
/// </summary>
public sealed class TempusDbContext : DbContext
{
    public const string Schema = "tempus";

    private Guid ScopeUserId { get; }

    private bool IsSystemScope { get; }

    public TempusDbContext(DbContextOptions<TempusDbContext> options, ICurrentUser currentUser) : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        ScopeUserId = currentUser.UserId ?? Guid.Empty;
        IsSystemScope = currentUser.IsSystem;
    }

    public DbSet<TimelineTask> TimelineTasks => Set<TimelineTask>();

    public DbSet<TimelineNote> TimelineNotes => Set<TimelineNote>();

    public DbSet<TimelineProposal> TimelineProposals => Set<TimelineProposal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<TimelineTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(TempusLimits.TitleMax);
            entity.Property(e => e.Description).HasMaxLength(TempusLimits.DescriptionMax);
            entity.Property(e => e.Priority).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Origin).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Tags).IsRequired();
            entity.Property(e => e.AssignedAgent).HasMaxLength(TempusLimits.AgentNameMax);
            entity.Property(e => e.CreatedByAgent).HasMaxLength(TempusLimits.AgentNameMax);

            entity.HasIndex(e => new { e.UserId, e.Year, e.Month });
            entity.HasIndex(e => new { e.UserId, e.Date });
            entity.HasIndex(e => e.Tags).HasMethod("gin");

            entity.ToTable(t => AddPlacementChecks(t, "TimelineTasks"));
            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<TimelineNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Author).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.AuthorName).HasMaxLength(TempusLimits.AgentNameMax);
            entity.Property(e => e.Content).IsRequired().HasMaxLength(TempusLimits.NoteMax);

            entity.HasIndex(e => new { e.UserId, e.Year, e.Month });

            entity.HasOne(e => e.Task)
                .WithMany(t => t.Notes)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            entity.ToTable(t => AddPlacementChecks(t, "TimelineNotes"));
            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<TimelineProposal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Priority).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(TempusLimits.TitleMax);
            entity.Property(e => e.Description).HasMaxLength(TempusLimits.DescriptionMax);
            entity.Property(e => e.Tags).IsRequired();
            entity.Property(e => e.AssignedAgent).HasMaxLength(TempusLimits.AgentNameMax);
            entity.Property(e => e.Rationale).HasMaxLength(TempusLimits.RationaleMax);
            entity.Property(e => e.ProposedBy).IsRequired().HasMaxLength(TempusLimits.AgentNameMax);

            entity.HasIndex(e => new { e.UserId, e.Status });
            entity.HasIndex(e => new { e.UserId, e.Year, e.Month });

            entity.HasOne(e => e.Task)
                .WithMany()
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            entity.ToTable(t => AddPlacementChecks(t, "TimelineProposals"));
            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
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

    /// <summary>
    /// Month in range and the day (when set) inside the row's month.
    /// </summary>
    private static void AddPlacementChecks<T>(TableBuilder<T> table, string name) where T : class
    {
        table.HasCheckConstraint($"CK_{name}_Month", "\"Month\" BETWEEN 1 AND 12");
        table.HasCheckConstraint(
            $"CK_{name}_DateInMonth",
            "\"Date\" IS NULL OR (EXTRACT(YEAR FROM \"Date\") = \"Year\" AND EXTRACT(MONTH FROM \"Date\") = \"Month\")");
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
    }
}
