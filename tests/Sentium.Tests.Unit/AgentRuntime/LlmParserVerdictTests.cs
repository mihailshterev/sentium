using FluentAssertions;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class LlmParserVerdictTests
{
    [Fact]
    public void StructuredStatusPassed_IsPassed()
        => LlmParser.ParseValidationVerdict("STATUS: PASSED\nCRITIQUE: None").Passed.Should().BeTrue();

    [Fact]
    public void StructuredStatusFailed_CapturesCritique()
    {
        var verdict = LlmParser.ParseValidationVerdict("STATUS: FAILED\nCRITIQUE: The translation is missing.");

        verdict.Passed.Should().BeFalse();
        verdict.Critique.Should().Be("The translation is missing.");
    }

    [Fact]
    public void NoStatusLine_AffirmativeOnly_IsPassed()
        => LlmParser.ParseValidationVerdict("Looks good to me, APPROVED.").Passed.Should().BeTrue();

    [Fact]
    public void NoStatusLine_AffirmativeButAlsoNegative_IsNotPassed()
        // Fail-safe: "would have PASSED but it FAILED" must not be read as an approval.
        => LlmParser.ParseValidationVerdict("This would have PASSED, but it ultimately FAILED on accuracy.").Passed.Should().BeFalse();

    [Fact]
    public void NoStatusLine_NoVerdictTokens_IsNotPassed()
        => LlmParser.ParseValidationVerdict("The output is incomplete and ignores the request.").Passed.Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOutput_IsNotPassed(string? output)
        => LlmParser.ParseValidationVerdict(output).Passed.Should().BeFalse();
}
