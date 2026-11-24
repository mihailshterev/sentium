using Microsoft.EntityFrameworkCore;
using Sentium.Vigil.Entities;

namespace Sentium.Vigil.Data;

/// <summary>
/// Inference metrics and incident history (schema <c>vigil</c>). Everything else Vigil knows (live health, devices)
/// lives in memory. The data is system-wide, not per user, so there are no query filters; the endpoints that read it are
/// Sovereign-only.
/// </summary>
internal sealed class VigilDbContext(DbContextOptions<VigilDbContext> options) : DbContext(options)
{
    public const string Schema = "vigil";

    private const int ModelMax = 200;
    private const int CallerMax = 64;
    private const int OperationMax = 32;
    private const int ServerMax = 16;
    private const int TargetMax = 200;
    private const int EnumNameMax = 32;

    public DbSet<InferenceServerMinute> InferenceServerMinutes => Set<InferenceServerMinute>();

    public DbSet<InferenceUsageMinute> InferenceUsageMinutes => Set<InferenceUsageMinute>();

    public DbSet<ModelLoadEvent> ModelLoadEvents => Set<ModelLoadEvent>();

    public DbSet<IncidentRecord> Incidents => Set<IncidentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<InferenceServerMinute>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Server).HasConversion<string>().HasMaxLength(ServerMax);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(ModelMax);
            entity.HasIndex(e => e.EpochMinute);
        });

        modelBuilder.Entity<InferenceUsageMinute>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Caller).IsRequired().HasMaxLength(CallerMax);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(ModelMax);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(OperationMax);
            entity.HasIndex(e => e.EpochMinute);
        });

        modelBuilder.Entity<ModelLoadEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Server).HasConversion<string>().HasMaxLength(ServerMax);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(ModelMax);
            entity.HasIndex(e => e.CompletedAt);
        });

        modelBuilder.Entity<IncidentRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Target).IsRequired().HasMaxLength(TargetMax);
            entity.Property(e => e.Kind).IsRequired().HasMaxLength(EnumNameMax);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(EnumNameMax);
            entity.Property(e => e.LastObservedStatus).IsRequired().HasMaxLength(EnumNameMax);
            entity.HasIndex(e => e.ResolvedAt);
        });
    }
}
