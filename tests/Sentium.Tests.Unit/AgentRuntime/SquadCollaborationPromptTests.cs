using FluentAssertions;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class SquadCollaborationPromptTests
{
    private static readonly string[] Roster = ["DotNet Architect", "NodeJS Developer", "Summarizer"];

    [Fact]
    public void MultiAgent_IncludesScopePositionRosterAndRules()
    {
        var d = SquadCollaborationPrompt.Build("NodeJS Developer", "Provide the NodeJS equivalent.", 2, 3, Roster);

        d.Should().Contain("step 2 of 3");
        d.Should().Contain("Provide the NodeJS equivalent.");
        d.Should().Contain("DotNet Architect -> NodeJS Developer -> Summarizer");
        d.Should().Contain("Do ONLY your assignment");
        d.Should().Contain("do not repeat");
        d.Should().Contain("a later step finalizes");
        d.Should().NotContain("### TASK");
    }

    [Fact]
    public void EmptyAssignment_FallsBackToRoleScope()
    {
        var d = SquadCollaborationPrompt.Build("DotNet Architect", null, 1, 3, Roster);

        d.Should().Contain("what your role (described above) covers");
    }

    [Fact]
    public void SingleAgent_ProducesFullAnswerDirective_NoPipelineHoldback()
    {
        var d = SquadCollaborationPrompt.Build("Solo", "the C# part", 1, 1, ["Solo"]);

        d.Should().Contain("### TASK");
        d.Should().Contain("complete, direct answer");
        d.Should().NotContain("PIPELINE ROLE");
        d.Should().NotContain("a later step finalizes");
    }
}
