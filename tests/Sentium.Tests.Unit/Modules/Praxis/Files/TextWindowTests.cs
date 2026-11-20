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

        Assert.Equal("two\nthree", slice.Text);
        Assert.Equivalent(new { StartLine = 2, EndLine = 3, TotalLines = 5, HasMore = true }, slice);
    }

    [Fact]
    public void Slice_StopsAtTheCharacterBudget_OnALineBoundary()
    {
        var slice = TextWindow.Slice(FiveLines, startLine: 1, maxLines: 100, maxChars: 8);

        Assert.Equal("one\ntwo", slice.Text);
        Assert.Equal(2, slice.EndLine);
        Assert.True(slice.HasMore);
    }

    [Fact]
    public void Slice_CutsASingleOverlongLine_SoPagingAlwaysAdvances()
    {
        var slice = TextWindow.Slice(new string('x', 50), startLine: 1, maxLines: 10, maxChars: 20);

        Assert.Equal(20, slice.Text.Length);
        Assert.Equal(1, slice.EndLine);
    }

    [Fact]
    public void Slice_ClampsAStartPastTheEnd_ToTheLastLine()
    {
        var slice = TextWindow.Slice(FiveLines, startLine: 99, maxLines: 10, maxChars: 1000);

        Assert.Equal("five", slice.Text);
        Assert.False(slice.HasMore);
    }

    [Theory]
    [InlineData(1_000, 4_000)]
    [InlineData(32_768, 45_875)]
    [InlineData(1_000_000, 160_000)]
    public void AttachmentBudget_ScalesWithTheContextWindow_WithinBounds(int contextWindowTokens, int expectedChars)
    {
        Assert.Equal(expectedChars, WorkspaceContextProviderFactory.BudgetFor(contextWindowTokens));
    }
}
