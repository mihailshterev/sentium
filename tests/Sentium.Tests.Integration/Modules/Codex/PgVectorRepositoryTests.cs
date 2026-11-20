using Microsoft.Extensions.DependencyInjection;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Codex.Data;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Codex;

/// <summary>
/// Exercises the pgvector-backed <see cref="IVectorRepository"/> against the real migrated <c>codex</c> schema.
/// Every test uses its own collection name so tests stay independent within the shared database.
/// </summary>
public sealed class PgVectorRepositoryTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string FileIdKey = "FileId";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_ReturnsNearestFirst_WithCosineScore()
    {
        var collection = NewCollection();
        await WithRepositoryAsync(async repo =>
        {
            await repo.UpsertAsync(collection, Chunk("exact"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("close"), Mix(0, 1, 0.9f), Ct);
            await repo.UpsertAsync(collection, Chunk("orthogonal"), Basis(2), Ct);

            var hits = await repo.SearchAsync(collection, Basis(0), topK: 3, ct: Ct);

            Assert.Equal(new[] { "exact", "close", "orthogonal" }, hits.Select(h => h.Chunk.Content));
            Assert.Equal(1f, hits[0].Score, tolerance: 0.0001f);
            Assert.Equal(0f, hits[2].Score, tolerance: 0.0001f);
        });
    }

    [Fact]
    public async Task Search_AppliesScoreThresholdAndTopK()
    {
        var collection = NewCollection();
        await WithRepositoryAsync(async repo =>
        {
            await repo.UpsertAsync(collection, Chunk("a"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("b"), Mix(0, 1, 0.9f), Ct);
            await repo.UpsertAsync(collection, Chunk("c"), Basis(1), Ct);

            Assert.Single(await repo.SearchAsync(collection, Basis(0), topK: 1, ct: Ct), h => h.Chunk.Content == "a");
            Assert.Equivalent(new string[] { "a", "b" }, (await repo.SearchAsync(collection, Basis(0), topK: 5, scoreThreshold: 0.5f, ct: Ct))
                .Select(h => h.Chunk.Content));
        });
    }

    [Fact]
    public async Task Search_RespectsKnowledgeScope()
    {
        var collection = NewCollection();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await WithRepositoryAsync(async repo =>
        {
            await repo.UpsertAsync(collection, Chunk("shared"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("alice", KnowledgeScope.User, alice), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("bob", KnowledgeScope.User, bob), Basis(0), Ct);

            async Task<IEnumerable<string>> Visible(KnowledgeScopeFilter? scope) =>
                (await repo.SearchAsync(collection, Basis(0), topK: 10, scope: scope, ct: Ct)).Select(h => h.Chunk.Content);

            Assert.Equivalent(new string[] { "shared", "alice" }, await Visible(new KnowledgeScopeFilter(alice)));
            Assert.Equivalent(new string[] { "shared" }, await Visible(new KnowledgeScopeFilter(null)));
            Assert.Equivalent(new string[] { "shared", "alice", "bob" }, await Visible(null));

            var page = await repo.GetPageAsync(collection, scope: new KnowledgeScopeFilter(bob), ct: Ct);
            Assert.Equivalent(new string[] { "shared", "bob" }, page.Select(c => c.Content));
        });
    }

    [Fact]
    public async Task Search_NarrowsToAMetadataValue_WithinTheUsersScope()
    {
        var collection = NewCollection();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await WithRepositoryAsync(async repo =>
        {
            await repo.UpsertAsync(collection, Tagged("alice ws-1", alice, "ws-1"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Tagged("alice ws-2", alice, "ws-2"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Tagged("bob ws-1", bob, "ws-1"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("shared, untagged"), Basis(0), Ct);

            var hits = await repo.SearchAsync(collection, Basis(0), topK: 10, scope: new KnowledgeScopeFilter(alice, "WorkspaceId", "ws-1"), ct: Ct);

            Assert.Equal(new string[] { "alice ws-1" }, hits.Select(h => h.Chunk.Content));
        });
    }

    [Fact]
    public async Task Upsert_RoundTripsAllFields_AndReplacesById()
    {
        var collection = NewCollection();
        var userId = Guid.NewGuid();
        var original = new DocumentChunk
        {
            Content = "v1",
            Source = "doc-1",
            SourceType = IngestionSourceType.File,
            Metadata = new Dictionary<string, string> { ["filename"] = "notes.md" },
            Scope = KnowledgeScope.User,
            UserId = userId
        };

        await WithRepositoryAsync(async repo =>
        {
            await repo.UpsertAsync(collection, original, Basis(0), Ct);
            await repo.UpsertAsync(collection, new DocumentChunk
            {
                Id = original.Id,
                Content = "v2",
                Source = original.Source,
                SourceType = original.SourceType,
                Metadata = original.Metadata,
                CreatedAt = original.CreatedAt,
                Scope = original.Scope,
                UserId = original.UserId
            }, Basis(1), Ct);

            var hit = Assert.Single(await repo.SearchAsync(collection, Basis(1), topK: 5, ct: Ct));
            Assert.Equal(1f, hit.Score, tolerance: 0.0001f);
            Assert.Equal(original.Id, hit.Chunk.Id);
            Assert.Equal("v2", hit.Chunk.Content);
            Assert.Equal(IngestionSourceType.File, hit.Chunk.SourceType);
            Assert.Equal("notes.md", Assert.Contains("filename", hit.Chunk.Metadata));
            Assert.Equal(KnowledgeScope.User, hit.Chunk.Scope);
            Assert.Equal(userId, hit.Chunk.UserId);
        });
    }

    [Fact]
    public async Task Upsert_RejectsWrongDimension()
    {
        await WithRepositoryAsync(async repo =>
        {
            var act = () => repo.UpsertAsync(NewCollection(), Chunk("x"), new float[3], Ct);
            await Assert.ThrowsAnyAsync<ArgumentException>(act);
        });
    }

    [Fact]
    public async Task DeleteBySource_RemovesOnlyThatSourceInThatCollection()
    {
        var collection = NewCollection();
        var other = NewCollection();

        await WithRepositoryAsync(async repo =>
        {
            await repo.UpsertAsync(collection, Chunk("a1", source: "a"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("a2", source: "a"), Basis(1), Ct);
            await repo.UpsertAsync(collection, Chunk("b1", source: "b"), Basis(2), Ct);
            await repo.UpsertAsync(other, Chunk("a-other", source: "a"), Basis(0), Ct);

            await repo.DeleteBySourceAsync(collection, "a", Ct);

            Assert.Equivalent(new string[] { "b1" }, (await repo.GetPageAsync(collection, ct: Ct)).Select(c => c.Content));
            Assert.Single(await repo.GetPageAsync(other, ct: Ct));
        });
    }

    [Fact]
    public async Task ReplaceByMetadata_SwapsOnlyThatDocumentsChunks()
    {
        var collection = NewCollection();

        await WithRepositoryAsync(async repo =>
        {
            await repo.AddRangeAsync(collection, [Embedded("old-1", "file-1", 0), Embedded("old-2", "file-1", 1), Embedded("keep", "file-2", 2)], Ct);

            await repo.ReplaceByMetadataAsync(collection, FileIdKey, "file-1", Batches([Embedded("new-1", "file-1", 3)], [Embedded("new-2", "file-1", 4)]), Ct);
            await repo.ReplaceByMetadataAsync(collection, FileIdKey, "file-1", Batches([Embedded("new-1", "file-1", 3)], [Embedded("new-2", "file-1", 4)]), Ct);

            Assert.Equivalent(new string[] { "keep", "new-1", "new-2" }, (await repo.GetPageAsync(collection, ct: Ct)).Select(c => c.Content));
        });
    }

    [Fact]
    public async Task ReplaceByMetadata_KeepsTheOldChunks_WhenABatchFails()
    {
        var collection = NewCollection();

        await WithRepositoryAsync(async repo =>
        {
            await repo.AddRangeAsync(collection, [Embedded("old", "file-1", 0)], Ct);

            var act = () => repo.ReplaceByMetadataAsync(collection, FileIdKey, "file-1", FailingBatches(Embedded("new", "file-1", 1)), Ct);

            await Assert.ThrowsAnyAsync<InvalidOperationException>(act);
            Assert.Equal(new string[] { "old" }, (await repo.GetPageAsync(collection, ct: Ct)).Select(c => c.Content));
        });
    }

    [Fact]
    public async Task DeleteByMetadata_RemovesOnlyMatchingChunks()
    {
        var collection = NewCollection();

        await WithRepositoryAsync(async repo =>
        {
            await repo.AddRangeAsync(collection, [Embedded("a", "file-1", 0), Embedded("b", "file-2", 1)], Ct);

            var deleted = await repo.DeleteByMetadataAsync(collection, FileIdKey, "file-1", Ct);

            Assert.Equal(1, deleted);
            Assert.Equal(new string[] { "b" }, (await repo.GetPageAsync(collection, ct: Ct)).Select(c => c.Content));
        });
    }

    [Fact]
    public async Task Stats_AreNullWhenEmpty_AndCountAfterInsert_AndClearedByDeleteCollection()
    {
        var collection = NewCollection();

        await WithRepositoryAsync(async repo =>
        {
            Assert.Null(await repo.GetCollectionStatsAsync(collection, Ct));

            await repo.UpsertAsync(collection, Chunk("a"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("b"), Basis(1), Ct);

            var stats = await repo.GetCollectionStatsAsync(collection, Ct);
            Assert.Equal(new CollectionStats(collection, 2, CodexDbContext.EmbeddingDimensions, CodexDbContext.DistanceMetric), stats);

            await repo.DeleteCollectionAsync(collection, Ct);
            Assert.Null(await repo.GetCollectionStatsAsync(collection, Ct));
        });
    }

    [Fact]
    public async Task GetPage_PagesOldestFirst_WithOffsetAndLimit()
    {
        var collection = NewCollection();
        var start = DateTimeOffset.UtcNow.AddMinutes(-10);

        await WithRepositoryAsync(async repo =>
        {
            for (var i = 0; i < 5; i++)
            {
                await repo.UpsertAsync(collection, Chunk($"c{i}", createdAt: start.AddMinutes(i)), Basis(i), Ct);
            }

            Assert.Equal(new string[] { "c0", "c1" }, (await repo.GetPageAsync(collection, limit: 2, ct: Ct)).Select(c => c.Content));
            Assert.Equal(new string[] { "c2", "c3" }, (await repo.GetPageAsync(collection, limit: 2, offset: 2, ct: Ct)).Select(c => c.Content));
            Assert.Equal(new string[] { "c4" }, (await repo.GetPageAsync(collection, limit: 2, offset: 4, ct: Ct)).Select(c => c.Content));
        });
    }

    private async Task WithRepositoryAsync(Func<IVectorRepository, Task> test)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await test(scope.ServiceProvider.GetRequiredService<IVectorRepository>());
    }

    private static string NewCollection() => $"test_{Guid.NewGuid():N}";

    private static DocumentChunk Chunk(string content, string scope = KnowledgeScope.Shared, Guid? userId = null, string source = "test-source", DateTimeOffset? createdAt = null)
    {
        return new()
        {
            Content = content,
            Source = source,
            SourceType = IngestionSourceType.Custom,
            Scope = scope,
            UserId = userId,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    private static DocumentChunk Tagged(string content, Guid? userId, string workspaceId)
    {
        return new()
        {
            Content = content,
            Source = "test-source",
            SourceType = IngestionSourceType.Custom,
            Scope = KnowledgeScope.User,
            UserId = userId,
            Metadata = new Dictionary<string, string> { ["WorkspaceId"] = workspaceId },
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static EmbeddedChunk Embedded(string content, string fileId, int dimension)
    {
        var chunk = new DocumentChunk
        {
            Content = content,
            Source = "test-source",
            SourceType = IngestionSourceType.File,
            Metadata = new Dictionary<string, string> { [FileIdKey] = fileId }
        };

        return new EmbeddedChunk(chunk, Basis(dimension));
    }

    private static async IAsyncEnumerable<IReadOnlyList<EmbeddedChunk>> Batches(params IReadOnlyList<EmbeddedChunk>[] batches)
    {
        foreach (var batch in batches)
        {
            await Task.Yield();
            yield return batch;
        }
    }

    private static async IAsyncEnumerable<IReadOnlyList<EmbeddedChunk>> FailingBatches(EmbeddedChunk first)
    {
        await Task.Yield();
        yield return [first];
        throw new InvalidOperationException("The embedding server went away.");
    }

    /// <summary>
    /// Unit vector along dimension <paramref name="i"/>.
    /// </summary>
    private static float[] Basis(int i)
    {
        var v = new float[CodexDbContext.EmbeddingDimensions];
        v[i] = 1f;
        return v;
    }

    /// <summary>
    /// Vector mostly along <paramref name="primary"/> with a <c>1 - weight</c> component along <paramref name="secondary"/>.
    /// </summary>
    private static float[] Mix(int primary, int secondary, float weight)
    {
        var v = new float[CodexDbContext.EmbeddingDimensions];
        v[primary] = weight;
        v[secondary] = 1f - weight;
        return v;
    }
}
