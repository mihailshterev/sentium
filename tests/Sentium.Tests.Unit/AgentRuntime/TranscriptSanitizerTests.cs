using FluentAssertions;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class TranscriptSanitizerTests
{
    [Fact]
    public void StripsStatusCompletedLine()
        => TranscriptSanitizer.StripHandoffLines("Here is my analysis.\nSTATUS: COMPLETED")
            .Should().Be("Here is my analysis.");

    [Fact]
    public void StripsMarkdownPrefixedNeedsActionLine()
        => TranscriptSanitizer.StripHandoffLines("Body text.\n- STATUS: NEEDS_ACTION please continue")
            .Should().Be("Body text.");

    [Fact]
    public void IsCaseInsensitive()
        => TranscriptSanitizer.StripHandoffLines("done here\nstatus: completed").Should().Be("done here");

    [Fact]
    public void LeavesRealContentUntouched()
        => TranscriptSanitizer.StripHandoffLines("The status of the system is green.")
            .Should().Be("The status of the system is green.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmpty_ReturnsEmpty(string? input)
        => TranscriptSanitizer.StripHandoffLines(input).Should().BeEmpty();
}
