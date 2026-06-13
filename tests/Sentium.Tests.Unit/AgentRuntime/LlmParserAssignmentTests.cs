using FluentAssertions;
using NSubstitute;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Core.Agents;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class LlmParserAssignmentTests
{
    private static (Dictionary<string, string> Db, IAgentRegistry Registry) Setup()
    {
        var db = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DotNet Architect"] = "C# architect",
            ["NodeJS Developer"] = "node dev",
        };
        var registry = Substitute.For<IAgentRegistry>();
        registry.GetRegisteredNames().Returns(new[] { "Summarizer", "Validator", "Orchestrator" });
        return (db, registry);
    }

    [Fact]
    public void ObjectArray_ReturnsAgentTaskPairsInOrder()
    {
        var (db, reg) = Setup();
        const string output = """
            [{"agent":"DotNet Architect","task":"Do the C# part."},{"agent":"NodeJS Developer","task":"Do the Node part."},{"agent":"Summarizer","task":"Combine."}]
            """;

        var result = LlmParser.ParseAgentAssignments(output, db, reg);

        result.Select(a => a.Agent).Should().Equal("DotNet Architect", "NodeJS Developer", "Summarizer");
        result[0].Task.Should().Be("Do the C# part.");
        result[2].Task.Should().Be("Combine.");
    }

    [Fact]
    public void FencedJson_IsParsed()
    {
        var (db, reg) = Setup();
        const string output = "```json\n[{\"agent\":\"Summarizer\",\"task\":\"Sum it up.\"}]\n```";

        var result = LlmParser.ParseAgentAssignments(output, db, reg);

        result.Should().ContainSingle();
        result[0].Agent.Should().Be("Summarizer");
        result[0].Task.Should().Be("Sum it up.");
    }

    [Fact]
    public void PlainStringArray_FallsBackToNamesWithEmptyTasks()
    {
        var (db, reg) = Setup();
        const string output = """["DotNet Architect", "Summarizer"]""";

        var result = LlmParser.ParseAgentAssignments(output, db, reg);

        result.Select(a => a.Agent).Should().Equal("DotNet Architect", "Summarizer");
        result.Should().OnlyContain(a => a.Task == "");
    }

    [Fact]
    public void UnknownAgentsDropped_AndDuplicatesRemovedCaseInsensitively()
    {
        var (db, reg) = Setup();
        const string output = """
            [{"agent":"Ghost","task":"x"},{"agent":"Summarizer","task":"a"},{"agent":"summarizer","task":"b"}]
            """;

        var result = LlmParser.ParseAgentAssignments(output, db, reg);

        result.Select(a => a.Agent).Should().Equal("Summarizer");
        result[0].Task.Should().Be("a");
    }

    [Theory]
    [InlineData("I cannot help with that.")]
    [InlineData("[]")]
    public void NoUsableAssignments_ReturnsEmpty(string output)
    {
        var (db, reg) = Setup();
        LlmParser.ParseAgentAssignments(output, db, reg).Should().BeEmpty();
    }
}
