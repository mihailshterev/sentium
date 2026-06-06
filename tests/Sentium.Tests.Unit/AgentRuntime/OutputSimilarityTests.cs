using System.Linq;
using FluentAssertions;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

/// <summary>
/// Covers <see cref="OutputSimilarity.IsStuck"/>, the stuck-state detector the refinement loop uses to abort
/// when a local model ignores the critique and re-emits a near-identical cumulative transcript.
/// </summary>
public sealed class OutputSimilarityTests
{
    [Fact]
    public void NullPrevious_IsNotStuck()
        => OutputSimilarity.IsStuck(null, "anything").Should().BeFalse();

    [Fact]
    public void IdenticalText_IsStuck()
        => OutputSimilarity.IsStuck("the quick brown fox", "the quick brown fox").Should().BeTrue();

    [Fact]
    public void IdenticalIgnoringPunctuationAndCase_IsStuck()
        => OutputSimilarity.IsStuck("The quick, BROWN fox!", "the quick brown fox").Should().BeTrue();

    [Fact]
    public void DisjointText_IsNotStuck()
        => OutputSimilarity.IsStuck("the quick brown fox", "completely separate unrelated phrasing entirely").Should().BeFalse();

    [Fact]
    public void NearIdenticalAboveThreshold_IsStuck()
    {
        // 20 shared tokens; current adds one extra -> Jaccard 20/21 ≈ 0.952 (>= 0.95) yet not byte-identical.
        var prev = string.Join(' ', Enumerable.Range(0, 20).Select(i => $"word{i}"));
        var curr = prev + " extraword";
        OutputSimilarity.IsStuck(prev, curr).Should().BeTrue();
    }

    [Fact]
    public void PartialOverlapBelowThreshold_IsNotStuck()
    {
        var prev = string.Join(' ', Enumerable.Range(0, 10).Select(i => $"word{i}"));
        var curr = string.Join(' ', Enumerable.Range(5, 10).Select(i => $"word{i}")); // ~33% Jaccard
        OutputSimilarity.IsStuck(prev, curr).Should().BeFalse();
    }
}
