using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Codex.Data;
using Sentium.Codex.Features.Rag;
using Sentium.Core.Constants;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Codex;

/// <summary>
/// After an embedding-model change, stored chunks are re-embedded in place from their stored content,
/// against the real migrated <c>codex</c> schema. A fake generator stands in for the embedding server.
/// </summary>
public sealed class EmbeddingReindexerTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string OldModel = "embeddinggemma-2:740m";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upsert_StampsTheConfiguredEmbeddingModel()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVectorRepository>();
        var db = scope.ServiceProvider.GetRequiredService<CodexDbContext>();
        var collection = NewCollection();

        await repo.UpsertAsync(collection, Chunk("stamped"), Basis(0), Ct);

        var row = await db.KnowledgeChunks.AsNoTracking().SingleAsync(c => c.Collection == collection, Ct);
        row.EmbeddingModel.Should().Be(AIModels.EmbeddingGemma2);
    }

    [Fact]
    public async Task ReindexBatchAsync_ReembedsOnlyChunksFromAnotherModel()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVectorRepository>();
        var db = scope.ServiceProvider.GetRequiredService<CodexDbContext>();
        await db.KnowledgeChunks.ExecuteDeleteAsync(Ct);

        var collection = NewCollection();
        await repo.UpsertAsync(collection, Chunk("stale one"), Basis(0), Ct);
        await repo.UpsertAsync(collection, Chunk("stale two"), Basis(1), Ct);
        await repo.UpsertAsync(collection, Chunk("fresh"), Basis(2), Ct);
        await MarkAsEmbeddedByAsync(db, OldModel, "stale one", "stale two");

        using var generator = new FakeEmbeddingGenerator(Basis(5));
        var reindexer = Reindexer(db, generator);

        (await reindexer.CountPendingAsync(Ct)).Should().Be(2);
        (await reindexer.ReindexBatchAsync(Ct)).Should().Be(2);
        (await reindexer.ReindexBatchAsync(Ct)).Should().Be(0, "every chunk now carries the configured model");

        generator.Inputs.Should().BeEquivalentTo("stale one", "stale two");

        db.ChangeTracker.Clear();
        var rows = await db.KnowledgeChunks.AsNoTracking().Where(c => c.Collection == collection).ToDictionaryAsync(c => c.Content, Ct);
        rows.Values.Should().OnlyContain(c => c.EmbeddingModel == AIModels.EmbeddingGemma2);
        rows["stale one"].Embedding.ToArray()[5].Should().Be(1f);
        rows["stale two"].Embedding.ToArray()[5].Should().Be(1f);
        rows["fresh"].Embedding.ToArray()[2].Should().Be(1f, "chunks already from the configured model are left alone");
    }

    [Fact]
    public async Task ReindexBatchAsync_Throws_AndKeepsTheRows_WhenTheModelReturnsTheWrongSize()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVectorRepository>();
        var db = scope.ServiceProvider.GetRequiredService<CodexDbContext>();
        await db.KnowledgeChunks.ExecuteDeleteAsync(Ct);

        var collection = NewCollection();
        await repo.UpsertAsync(collection, Chunk("stale"), Basis(0), Ct);
        await MarkAsEmbeddedByAsync(db, OldModel, "stale");

        using var wrongSize = new FakeEmbeddingGenerator([0.1f, 0.2f, 0.3f]);
        var reindexer = Reindexer(db, wrongSize);

        var act = () => reindexer.ReindexBatchAsync(Ct);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*768*");
        db.ChangeTracker.Clear();
        (await db.KnowledgeChunks.AsNoTracking().SingleAsync(c => c.Collection == collection, Ct)).EmbeddingModel.Should().Be(OldModel);
    }

    private static EmbeddingReindexer Reindexer(CodexDbContext db, IEmbeddingGenerator<string, Embedding<float>> generator) =>
        new(db, generator, Options.Create(new RagOptions { EmbeddingModelName = AIModels.EmbeddingGemma2 }), NullLogger<EmbeddingReindexer>.Instance);

    private static async Task MarkAsEmbeddedByAsync(CodexDbContext db, string model, params string[] contents)
    {
        foreach (var content in contents)
        {
            await db.Database.ExecuteSqlAsync(
                $"""UPDATE codex.knowledge_chunks SET "EmbeddingModel" = {model} WHERE "Content" = {content}""", Ct);
        }

        // Raw SQL bypasses the change tracker; drop the entities the repository tracked so the reindexer sees
        // the database state, as it does in production (a fresh scope per batch).
        db.ChangeTracker.Clear();
    }

    private static string NewCollection() => $"test_{Guid.NewGuid():N}";

    private static DocumentChunk Chunk(string content)
    {
        return new()
        {
            Content = content,
            Source = "test-source",
            SourceType = IngestionSourceType.Custom,
            Scope = KnowledgeScope.Shared,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static float[] Basis(int i)
    {
        var v = new float[CodexDbContext.EmbeddingDimensions];
        v[i] = 1f;
        return v;
    }

    private sealed class FakeEmbeddingGenerator(float[] vector) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public List<string> Inputs { get; } = [];

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var batch = values.ToList();
            Inputs.AddRange(batch);
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(batch.Select(_ => new Embedding<float>(vector))));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
