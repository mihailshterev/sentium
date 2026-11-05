using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sentium.Codex.Entities;

namespace Sentium.Codex.Data;

/// <summary>
/// Codex's vector store: knowledge-base, learning and memory embeddings in PostgreSQL via pgvector.
/// </summary>
public sealed class CodexDbContext(DbContextOptions<CodexDbContext> options) : DbContext(options)
{
    public const string Schema = "codex";

    /// <summary>
    /// Dimensionality of the <c>embedding</c> column. Must match the embedding model output; changing the
    /// embedding model to one with another size needs a migration (and a re-ingest).
    /// </summary>
    public const int EmbeddingDimensions = 768;

    public const string DistanceMetric = "Cosine";

    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<KnowledgeChunk> KnowledgeChunks { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);
        builder.HasPostgresExtension("vector");

        builder.Entity<KnowledgeChunk>(entity =>
        {
            entity.ToTable("knowledge_chunks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.Property(e => e.Collection).HasMaxLength(64);
            entity.Property(e => e.Source).HasMaxLength(1024);
            entity.Property(e => e.SourceType).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Scope).HasMaxLength(16);

            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, MetadataJsonOptions),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, MetadataJsonOptions) ?? new Dictionary<string, string>(),
                    new ValueComparer<Dictionary<string, string>>(
                        (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                        d => d.Aggregate(0, (hash, kvp) => HashCode.Combine(hash, kvp.Key, kvp.Value)),
                        d => new Dictionary<string, string>(d)))
                .HasColumnType("jsonb");

            entity.Property(e => e.Embedding).HasColumnType($"vector({EmbeddingDimensions})");

            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            entity.HasIndex(e => new { e.Collection, e.Source });
            entity.HasIndex(e => new { e.Collection, e.UserId });
            entity.HasIndex(e => new { e.Collection, e.CreatedAt });
        });
    }
}
