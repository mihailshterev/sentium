using FluentAssertions;
using NSubstitute;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Orchestration;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Workflows;

public sealed class WorkflowTimelineTests
{
    private readonly IAgentStreamPublisher _stream = Substitute.For<IAgentStreamPublisher>();

    [Fact]
    public void Post_PublishesToTheRunStream_AndLogsTheEntry()
    {
        var timeline = new WorkflowTimeline(_stream, "s1");

        timeline.Post(WorkflowTimeline.SystemAuthor, "Planning", AgentUpdateTypes.Status);

        _stream.Received(1).Publish("s1", new AgentStreamUpdate(WorkflowTimeline.SystemAuthor, "Planning", AgentUpdateTypes.Status));
        timeline.Entries.Select(e => (e.Author, e.Text, e.Type)).Should().Equal(
            (WorkflowTimeline.SystemAuthor, "Planning", AgentUpdateTypes.Status));
    }

    [Fact]
    public async Task For_PostsAgentUpdatesUnderTheAuthor_AndMergesTextInTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var timeline = new WorkflowTimeline(_stream, "s1");
        var sink = timeline.For("Researcher");

        await sink.WriteUpdateAsync(AgentUpdateTypes.Message, "Hel", ct);
        await sink.WriteUpdateAsync(AgentUpdateTypes.Message, "lo", ct);
        await sink.WriteUpdateAsync(AgentUpdateTypes.ToolCall, "{}", ct);

        _stream.Received(1).Publish("s1", new AgentStreamUpdate("Researcher", "Hel", AgentUpdateTypes.Message));
        _stream.Received(1).Publish("s1", new AgentStreamUpdate("Researcher", "{}", AgentUpdateTypes.ToolCall));
        timeline.Entries.Select(e => (e.Author, e.Text, e.Type)).Should().Equal(
            ("Researcher", "Hello", AgentUpdateTypes.Message),
            ("Researcher", "{}", AgentUpdateTypes.ToolCall));
    }
}
