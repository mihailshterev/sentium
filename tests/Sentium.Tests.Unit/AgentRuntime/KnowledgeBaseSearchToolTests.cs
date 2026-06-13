using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
using Sentium.AgentRuntime.Infrastructure.Tools;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

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
        return new KnowledgeBaseSearchTool(_embedding, _repo, options, _pdp, Substitute.For<ILogger<KnowledgeBaseSearchTool>>());
    }

    private void MockStore(string collection, string content, float score) =>
        _repo.SearchAsync(collection, Arg.Any<float[]>(), Arg.Any<int>(), Arg.Any<float>(), Arg.Any<KnowledgeScopeFilter?>(), Arg.Any<CancellationToken>())
            .Returns(new VectorSearchResult[]
            {
                new() { Score = score, Chunk = new DocumentChunk { Content = content, Source = collection } }
            });

    [Fact]
    public async Task SearchesAllStores_MergesSortsByScore_AndLabelsBySource()
    {
        var ct = TestContext.Current.CancellationToken;
        MockStore(KnowledgeCollections.KnowledgeBase, "kb snippet", 0.90f);
        MockStore(KnowledgeCollections.AgentLearnings, "learning snippet", 0.95f);
        MockStore(KnowledgeCollections.UserMemories, "memory snippet", 0.80f);

        var result = await CreateTool().ExecuteAsync("clean architecture", ct);

        result.Should().Contain("kb snippet").And.Contain("learning snippet").And.Contain("memory snippet");
        result.Should().Contain("Knowledge Base").And.Contain("Captured Learning").And.Contain("Saved Memory");

        // Highest score first: learning (0.95) > kb (0.90) > memory (0.80).
        result.IndexOf("learning snippet", StringComparison.Ordinal)
            .Should().BeLessThan(result.IndexOf("kb snippet", StringComparison.Ordinal));
        result.IndexOf("kb snippet", StringComparison.Ordinal)
            .Should().BeLessThan(result.IndexOf("memory snippet", StringComparison.Ordinal));

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

        var result = await CreateTool().ExecuteAsync("query", ct);

        result.Should().Contain("kb snippet");
    }

    [Fact]
    public async Task NoResultsAcrossAllStores_ReturnsNotFoundMessage()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await CreateTool().ExecuteAsync("query", ct);

        result.Should().Contain("No relevant information");
    }

    [Fact]
    public async Task TopK_LimitsMergedResultsToHighestScoring()
    {
        var ct = TestContext.Current.CancellationToken;
        MockStore(KnowledgeCollections.KnowledgeBase, "kb snippet", 0.90f);
        MockStore(KnowledgeCollections.AgentLearnings, "learning snippet", 0.95f);
        MockStore(KnowledgeCollections.UserMemories, "memory snippet", 0.80f);

        var result = await CreateTool().ExecuteAsync("""{"query":"x","topK":1}""", ct);

        result.Should().Contain("Snippet 1").And.NotContain("Snippet 2");
        result.Should().Contain("learning snippet");
        result.Should().NotContain("memory snippet");
    }
}

public sealed class RagOptionsTests
{
    [Fact]
    public void SearchCollections_DefaultsToAllThreeStores()
        => new RagOptions().SearchCollections.Should().BeEquivalentTo(new[] { "knowledge_base", "agent_learnings", "user_memories" });
}
