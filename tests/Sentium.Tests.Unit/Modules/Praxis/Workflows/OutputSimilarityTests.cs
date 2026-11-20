using Sentium.Praxis.Features.Workflows.Squad;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Workflows;

/// <summary>
/// Covers <see cref="OutputSimilarity.IsStuck"/>, the stuck-state detector the refinement loop uses to abort
/// when a local model ignores the critique and re-emits a near-identical cumulative transcript.
/// </summary>
public sealed class OutputSimilarityTests
{
    [Fact]
    public void NullPrevious_IsNotStuck()
        => Assert.False(OutputSimilarity.IsStuck(null, "anything"));

    [Fact]
    public void IdenticalText_IsStuck()
        => Assert.True(OutputSimilarity.IsStuck("the quick brown fox", "the quick brown fox"));

    [Fact]
    public void IdenticalIgnoringPunctuationAndCase_IsStuck()
        => Assert.True(OutputSimilarity.IsStuck("The quick, BROWN fox!", "the quick brown fox"));

    [Fact]
    public void DisjointText_IsNotStuck()
        => Assert.False(OutputSimilarity.IsStuck("the quick brown fox", "completely separate unrelated phrasing entirely"));

    [Fact]
    public void NearIdenticalAboveThreshold_IsStuck()
    {
        // 20 shared tokens; current adds one extra -> Jaccard 20/21 ≈ 0.952 (>= 0.95) yet not byte-identical.
        var prev = string.Join(' ', Enumerable.Range(0, 20).Select(i => $"word{i}"));
        var curr = prev + " extraword";
        Assert.True(OutputSimilarity.IsStuck(prev, curr));
    }

    [Fact]
    public void PartialOverlapBelowThreshold_IsNotStuck()
    {
        var prev = string.Join(' ', Enumerable.Range(0, 10).Select(i => $"word{i}"));
        var curr = string.Join(' ', Enumerable.Range(5, 10).Select(i => $"word{i}")); // ~33% Jaccard
        Assert.False(OutputSimilarity.IsStuck(prev, curr));
    }
}
