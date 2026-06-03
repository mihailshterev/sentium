using FluentAssertions;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Xunit;

namespace Sentium.Tests.Unit.AgentRuntime;

public sealed class SquadSlicingTests
{
    private static readonly string[] Squad = ["Storyteller", "Translator", "Editor"];

    [Fact]
    public void NoneFlagged_ReturnsMinusOne_SignallingFullSquad()
        => SquadSlicing.ComputeReflowStartIndex(Squad, []).Should().Be(-1);

    [Fact]
    public void MiddleFlagged_ReturnsItsIndex_SoDownstreamReflows()
        => SquadSlicing.ComputeReflowStartIndex(Squad, ["Translator"]).Should().Be(1);

    [Fact]
    public void LastFlagged_ReturnsLastIndex()
        => SquadSlicing.ComputeReflowStartIndex(Squad, ["Editor"]).Should().Be(2);

    [Fact]
    public void FirstFlagged_ReturnsZero_FullReflow()
        => SquadSlicing.ComputeReflowStartIndex(Squad, ["Storyteller"]).Should().Be(0);

    [Fact]
    public void MultipleFlagged_ReturnsEarliestIndex()
        => SquadSlicing.ComputeReflowStartIndex(Squad, ["Editor", "Translator"]).Should().Be(1);

    [Fact]
    public void UnknownName_IsIgnored()
        => SquadSlicing.ComputeReflowStartIndex(Squad, ["Researcher"]).Should().Be(-1);

    [Fact]
    public void MatchingIsCaseInsensitive()
        => SquadSlicing.ComputeReflowStartIndex(Squad, ["translator"]).Should().Be(1);

    [Fact]
    public void EmptySquad_ReturnsMinusOne()
        => SquadSlicing.ComputeReflowStartIndex([], ["Translator"]).Should().Be(-1);
}
