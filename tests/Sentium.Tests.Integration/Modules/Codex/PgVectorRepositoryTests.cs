using FluentAssertions;
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

            hits.Select(h => h.Chunk.Content).Should().ContainInOrder("exact", "close", "orthogonal");
            hits[0].Score.Should().BeApproximately(1f, 0.0001f);
            hits[2].Score.Should().BeApproximately(0f, 0.0001f);
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

            (await repo.SearchAsync(collection, Basis(0), topK: 1, ct: Ct)).Should().ContainSingle(h => h.Chunk.Content == "a");
            (await repo.SearchAsync(collection, Basis(0), topK: 5, scoreThreshold: 0.5f, ct: Ct))
                .Select(h => h.Chunk.Content).Should().BeEquivalentTo("a", "b");
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

            (await Visible(new KnowledgeScopeFilter(alice))).Should().BeEquivalentTo("shared", "alice");
            (await Visible(new KnowledgeScopeFilter(null))).Should().BeEquivalentTo("shared");
            (await Visible(null)).Should().BeEquivalentTo("shared", "alice", "bob");

            var page = await repo.GetPageAsync(collection, scope: new KnowledgeScopeFilter(bob), ct: Ct);
            page.Select(c => c.Content).Should().BeEquivalentTo("shared", "bob");
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

            var hit = (await repo.SearchAsync(collection, Basis(1), topK: 5, ct: Ct)).Should().ContainSingle().Subject;
            hit.Score.Should().BeApproximately(1f, 0.0001f);
            hit.Chunk.Id.Should().Be(original.Id);
            hit.Chunk.Content.Should().Be("v2");
            hit.Chunk.SourceType.Should().Be(IngestionSourceType.File);
            hit.Chunk.Metadata.Should().Contain("filename", "notes.md");
            hit.Chunk.Scope.Should().Be(KnowledgeScope.User);
            hit.Chunk.UserId.Should().Be(userId);
        });
    }

    [Fact]
    public async Task Upsert_RejectsWrongDimension()
    {
        await WithRepositoryAsync(async repo =>
        {
            var act = () => repo.UpsertAsync(NewCollection(), Chunk("x"), new float[3], Ct);
            await act.Should().ThrowAsync<ArgumentException>();
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

            (await repo.GetPageAsync(collection, ct: Ct)).Select(c => c.Content).Should().BeEquivalentTo("b1");
            (await repo.GetPageAsync(other, ct: Ct)).Should().ContainSingle();
        });
    }

    [Fact]
    public async Task Stats_AreNullWhenEmpty_AndCountAfterInsert_AndClearedByDeleteCollection()
    {
        var collection = NewCollection();

        await WithRepositoryAsync(async repo =>
        {
            (await repo.GetCollectionStatsAsync(collection, Ct)).Should().BeNull();

            await repo.UpsertAsync(collection, Chunk("a"), Basis(0), Ct);
            await repo.UpsertAsync(collection, Chunk("b"), Basis(1), Ct);

            var stats = await repo.GetCollectionStatsAsync(collection, Ct);
            stats.Should().Be(new CollectionStats(collection, 2, CodexDbContext.EmbeddingDimensions, CodexDbContext.DistanceMetric));

            await repo.DeleteCollectionAsync(collection, Ct);
            (await repo.GetCollectionStatsAsync(collection, Ct)).Should().BeNull();
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

            (await repo.GetPageAsync(collection, limit: 2, ct: Ct)).Select(c => c.Content).Should().Equal("c0", "c1");
            (await repo.GetPageAsync(collection, limit: 2, offset: 2, ct: Ct)).Select(c => c.Content).Should().Equal("c2", "c3");
            (await repo.GetPageAsync(collection, limit: 2, offset: 4, ct: Ct)).Select(c => c.Content).Should().Equal("c4");
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
