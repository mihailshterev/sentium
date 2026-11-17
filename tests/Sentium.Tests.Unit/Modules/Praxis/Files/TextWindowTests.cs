using FluentAssertions;
using Sentium.Praxis.Features.Files;
using Sentium.Praxis.Features.Workspaces;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Files;

public sealed class TextWindowTests
{
    private const string FiveLines = "one\ntwo\r\nthree\nfour\nfive";

    [Fact]
    public void Slice_ReturnsTheRequestedLineRange()
    {
        var slice = TextWindow.Slice(FiveLines, startLine: 2, maxLines: 2, maxChars: 1000);

        slice.Text.Should().Be("two\nthree");
        slice.Should().BeEquivalentTo(new { StartLine = 2, EndLine = 3, TotalLines = 5, HasMore = true });
    }

    [Fact]
    public void Slice_StopsAtTheCharacterBudget_OnALineBoundary()
    {
        var slice = TextWindow.Slice(FiveLines, startLine: 1, maxLines: 100, maxChars: 8);

        slice.Text.Should().Be("one\ntwo");
        slice.EndLine.Should().Be(2);
        slice.HasMore.Should().BeTrue();
    }

    [Fact]
    public void Slice_CutsASingleOverlongLine_SoPagingAlwaysAdvances()
    {
        var slice = TextWindow.Slice(new string('x', 50), startLine: 1, maxLines: 10, maxChars: 20);

        slice.Text.Should().HaveLength(20);
        slice.EndLine.Should().Be(1);
    }

    [Fact]
    public void Slice_ClampsAStartPastTheEnd_ToTheLastLine()
    {
        var slice = TextWindow.Slice(FiveLines, startLine: 99, maxLines: 10, maxChars: 1000);

        slice.Text.Should().Be("five");
        slice.HasMore.Should().BeFalse();
    }

    [Theory]
    [InlineData(1_000, 4_000)]
    [InlineData(32_768, 45_875)]
    [InlineData(1_000_000, 160_000)]
    public void AttachmentBudget_ScalesWithTheContextWindow_WithinBounds(int contextWindowTokens, int expectedChars)
    {
        WorkspaceContextProviderFactory.BudgetFor(contextWindowTokens).Should().Be(expectedChars);
    }
}
