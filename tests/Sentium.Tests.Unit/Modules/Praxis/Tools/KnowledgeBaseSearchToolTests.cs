using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Data;
using Sentium.Praxis.Features.Tools;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

public sealed class KnowledgeBaseSearchToolTests
{
    private readonly IEmbeddingService _embedding = Substitute.For<IEmbeddingService>();
    private readonly IVectorRepository _repo = Substitute.For<IVectorRepository>();
    private readonly IPdpContextAccessor _pdp = Substitute.For<IPdpContextAccessor>();
    private readonly RagOptions _options = new() { DefaultTopK = 5, ScoreThreshold = 0.5f };

    public KnowledgeBaseSearchToolTests()
    {
        _embedding.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { 0.1f, 0.2f });
        // Default: every collection returns nothing unless a test overrides it.
        _repo.SearchAsync(Arg.Any<string>(), Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<KnowledgeScopeFilter?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<VectorSearchResult>());
    }

    private KnowledgeBaseSearchTool CreateTool()
    {
        var options = Substitute.For<IOptions<RagOptions>>();
        options.Value.Returns(_options);
        return new KnowledgeBaseSearchTool(_embedding, _repo, options, _pdp, new PraxisDbContextFactory().CreateDbContext([]), Substitute.For<ILogger<KnowledgeBaseSearchTool>>());
    }

    private void MockStore(string collection, string content, float score)
    {
        _repo.SearchAsync(collection, Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<KnowledgeScopeFilter?>(), Arg.Any<CancellationToken>())
            .Returns(new VectorSearchResult[]
            {
                new() { Score = score, Chunk = new DocumentChunk { Content = content, Source = collection } }
            });
    }

    [Fact]
    public async Task SearchesAllStores_MergesSortsByScore_AndLabelsBySource()
    {
        var ct = TestContext.Current.CancellationToken;
        MockStore(KnowledgeCollections.KnowledgeBase, "kb snippet", 0.90f);
        MockStore(KnowledgeCollections.AgentLearnings, "learning snippet", 0.95f);
        MockStore(KnowledgeCollections.UserMemories, "memory snippet", 0.80f);

        var result = await CreateTool().SearchAsync("clean architecture", ct: ct);

        Assert.Contains("kb snippet", result, StringComparison.Ordinal);
        Assert.Contains("learning snippet", result, StringComparison.Ordinal);
        Assert.Contains("memory snippet", result, StringComparison.Ordinal);
        Assert.Contains("Knowledge Base", result, StringComparison.Ordinal);
        Assert.Contains("Captured Learning", result, StringComparison.Ordinal);
        Assert.Contains("Saved Memory", result, StringComparison.Ordinal);

        // Highest score first: learning (0.95) > kb (0.90) > memory (0.80).
        Assert.True(result.IndexOf("learning snippet", StringComparison.Ordinal) < result.IndexOf("kb snippet", StringComparison.Ordinal));
        Assert.True(result.IndexOf("kb snippet", StringComparison.Ordinal) < result.IndexOf("memory snippet", StringComparison.Ordinal));

        foreach (var collection in KnowledgeCollections.All)
        {
            await _repo.Received(1).SearchAsync(collection, Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<KnowledgeScopeFilter?>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task SkipsCollectionThatThrows_StillReturnsOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        MockStore(KnowledgeCollections.KnowledgeBase, "kb snippet", 0.9f);
        _repo.SearchAsync(KnowledgeCollections.UserMemories, Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<KnowledgeScopeFilter?>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<VectorSearchResult>>(_ => throw new InvalidOperationException("collection missing"));

        var result = await CreateTool().SearchAsync("query", ct: ct);

        Assert.Contains("kb snippet", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoResultsAcrossAllStores_ReturnsNotFoundMessage()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await CreateTool().SearchAsync("query", ct: ct);

        Assert.Contains("No relevant information", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TopK_LimitsMergedResultsToHighestScoring()
    {
        var ct = TestContext.Current.CancellationToken;
        MockStore(KnowledgeCollections.KnowledgeBase, "kb snippet", 0.90f);
        MockStore(KnowledgeCollections.AgentLearnings, "learning snippet", 0.95f);
        MockStore(KnowledgeCollections.UserMemories, "memory snippet", 0.80f);

        var result = await CreateTool().SearchAsync("x", 1, ct: ct);

        Assert.Contains("Snippet 1", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Snippet 2", result, StringComparison.Ordinal);
        Assert.Contains("learning snippet", result, StringComparison.Ordinal);
        Assert.DoesNotContain("memory snippet", result, StringComparison.Ordinal);
    }
}
