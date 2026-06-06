using FluentAssertions;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class LlmParserResponsibleAgentsTests
{
    private static readonly string[] Squad = ["Storyteller", "Translator", "Editor"];

    [Fact]
    public void StructuredLine_NamesSingleAgent_ReturnsThatAgent()
    {
        const string output = """
            STATUS: FAILED
            CRITIQUE: The translation is missing.
            RESPONSIBLE_AGENTS: Translator
            SUMMARY: A story with no translation.
            """;

        LlmParser.ParseResponsibleAgents(output, Squad).Should().Equal("Translator");
    }

    [Fact]
    public void StructuredLine_NamesMultipleAgents_PreservesSquadOrder()
    {
        // Listed Editor-first, but the result must follow squad order (Translator before Editor).
        const string output = """
            STATUS: FAILED
            CRITIQUE: Two agents went off-script.
            RESPONSIBLE_AGENTS: Editor, Translator
            """;

        LlmParser.ParseResponsibleAgents(output, Squad).Should().Equal("Translator", "Editor");
    }

    [Fact]
    public void StructuredLine_None_ReturnsEmptyForFullSquadFallback()
    {
        const string output = """
            STATUS: FAILED
            CRITIQUE: The answer is incomplete overall.
            RESPONSIBLE_AGENTS: None
            """;

        LlmParser.ParseResponsibleAgents(output, Squad).Should().BeEmpty();
    }

    [Fact]
    public void NoStructuredLine_FallsBackToScanningCritique()
    {
        const string output = """
            STATUS: FAILED
            CRITIQUE: The Translator produced original prose instead of translating.
            SUMMARY: Off-role output.
            """;

        LlmParser.ParseResponsibleAgents(output, Squad).Should().Equal("Translator");
    }

    [Fact]
    public void CritiqueWithNoKnownNames_ReturnsEmpty()
    {
        const string output = """
            STATUS: FAILED
            CRITIQUE: The output is too short and ignores the request.
            """;

        LlmParser.ParseResponsibleAgents(output, Squad).Should().BeEmpty();
    }

    [Fact]
    public void IgnoresNamesNotInSquad()
    {
        const string output = """
            STATUS: FAILED
            RESPONSIBLE_AGENTS: Translator, Researcher
            """;

        LlmParser.ParseResponsibleAgents(output, Squad).Should().Equal("Translator");
    }

    [Fact]
    public void WordAware_DoesNotMatchPartialName()
    {
        // "Editorial" must not match the "Editor" agent.
        const string output = """
            STATUS: FAILED
            CRITIQUE: The piece lacks editorial polish but the content is correct.
            """;

        LlmParser.ParseResponsibleAgents(output, Squad).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmptyOutput_ReturnsEmpty(string? output)
    {
        LlmParser.ParseResponsibleAgents(output, Squad).Should().BeEmpty();
    }

    [Fact]
    public void EmptySquad_ReturnsEmpty()
    {
        LlmParser.ParseResponsibleAgents("RESPONSIBLE_AGENTS: Translator", []).Should().BeEmpty();
    }

    [Fact]
    public void Verdict_CritiqueStopsBeforeResponsibleAgentsLine()
    {
        // Guards the CritiqueRegex change: the underscore-bearing RESPONSIBLE_AGENTS header must terminate the critique.
        const string output = """
            STATUS: FAILED
            CRITIQUE: Translation missing.
            RESPONSIBLE_AGENTS: Translator
            SUMMARY: ignore me
            """;

        var verdict = LlmParser.ParseValidationVerdict(output);

        verdict.Passed.Should().BeFalse();
        verdict.Critique.Should().Be("Translation missing.");
    }
}
