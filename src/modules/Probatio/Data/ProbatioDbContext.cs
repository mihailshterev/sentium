using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentium.Core.Common;
using Sentium.Infrastructure.Security;
using Sentium.Probatio.Common;
using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Data;

/// <summary>
/// Evaluation store (schema <c>probatio</c>). Eval Suites and Evaluations are private to their owner and, like Tempus, a Sovereign
/// does NOT bypass the per-user filter; only system scopes (migrations, the Evaluation worker, user removal) see all rows.
/// </summary>
internal sealed class ProbatioDbContext : DbContext
{
    public const string Schema = "probatio";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Guid ScopeUserId { get; }

    private bool IsSystemScope { get; }

    public ProbatioDbContext(DbContextOptions<ProbatioDbContext> options, ICurrentUser currentUser) : base(options)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        ScopeUserId = currentUser.UserId ?? Guid.Empty;
        IsSystemScope = currentUser.IsSystem;
    }

    public DbSet<EvalSuite> EvalSuites => Set<EvalSuite>();

    public DbSet<EvalSuiteVersion> EvalSuiteVersions => Set<EvalSuiteVersion>();

    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    public DbSet<EvaluationCaseResult> EvaluationCaseResults => Set<EvaluationCaseResult>();

    public DbSet<Benchmark> Benchmarks => Set<Benchmark>();

    public static string SerializeCases(List<EvalCase> cases) => JsonSerializer.Serialize(cases, JsonOptions);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<EvalSuite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(ProbatioLimits.SuiteNameMax);
            entity.Property(e => e.Description).HasMaxLength(ProbatioLimits.SuiteDescriptionMax);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasIndex(e => new { e.UserId, e.UpdatedAt });

            entity.HasMany(e => e.Versions)
                .WithOne(v => v.Suite)
                .HasForeignKey(v => v.SuiteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<EvalSuiteVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            AsJson(entity.Property(e => e.Cases));

            entity.HasIndex(e => new { e.SuiteId, e.Version }).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.ToTable(t => t.HasCheckConstraint("CK_EvalSuiteVersions_Version", "\"Version\" >= 1"));
            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        ConfigureEvaluations(builder);
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

    private static PropertyBuilder<T> AsJson<T>(PropertyBuilder<T> property) where T : class
    {
        return property
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                json => JsonSerializer.Deserialize<T>(json, JsonOptions)!,
                new ValueComparer<T>(
                    (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                    v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(StringComparison.Ordinal),
                    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, JsonOptions), JsonOptions)!))
            .HasColumnType("jsonb");
    }

    private void ConfigureEvaluations(ModelBuilder builder)
    {
        builder.Entity<Evaluation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SuiteName).IsRequired().HasMaxLength(ProbatioLimits.SuiteNameMax);
            entity.Property(e => e.AgentName).IsRequired().HasMaxLength(ProbatioLimits.AgentNameMax);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(ProbatioLimits.ModelNameMax);
            entity.Property(e => e.JudgeModel).HasMaxLength(ProbatioLimits.ModelNameMax);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.Error).HasMaxLength(ProbatioLimits.ErrorMax);
            AsJson(entity.Property(e => e.Cases));
            AsJson(entity.Property(e => e.Agent));

            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => new { e.UserId, e.SuiteId });
            entity.HasIndex(e => e.Status);

            entity.HasMany<EvaluationCaseResult>()
                .WithOne()
                .HasForeignKey(r => r.EvaluationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<Benchmark>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SuiteName).IsRequired().HasMaxLength(ProbatioLimits.SuiteNameMax);
            entity.Property(e => e.AgentName).IsRequired().HasMaxLength(ProbatioLimits.AgentNameMax);
            entity.Property(e => e.JudgeModel).HasMaxLength(ProbatioLimits.ModelNameMax);

            entity.HasIndex(e => new { e.UserId, e.CreatedAt });

            entity.HasMany<Evaluation>()
                .WithOne()
                .HasForeignKey(e => e.BenchmarkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });

        builder.Entity<EvaluationCaseResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            AsJson(entity.Property(e => e.Repetitions));

            entity.HasIndex(e => new { e.EvaluationId, e.CaseId }).IsUnique();

            entity.HasQueryFilter(e => IsSystemScope || e.UserId == ScopeUserId);
        });
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
