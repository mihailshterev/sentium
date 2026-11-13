using FluentAssertions;
using NSubstitute;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Workflows;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Streaming;

public sealed class RelayUpdateSinkTests
{
    [Fact]
    public async Task WriteUpdateAsync_PublishesToTheRunStream_AndMergesTextIntoTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var stream = Substitute.For<IAgentStreamPublisher>();
        var log = new StreamLogAccumulator();
        var sink = new RelayUpdateSink(stream, log, "s1", "Researcher");

        await sink.WriteUpdateAsync(AgentUpdateTypes.Message, "Hel", ct);
        await sink.WriteUpdateAsync(AgentUpdateTypes.Message, "lo", ct);
        await sink.WriteUpdateAsync(AgentUpdateTypes.ToolCall, "{}", ct);

        stream.Received(1).Publish("s1", new AgentStreamUpdate("Researcher", "Hel", AgentUpdateTypes.Message));
        stream.Received(1).Publish("s1", new AgentStreamUpdate("Researcher", "{}", AgentUpdateTypes.ToolCall));
        log.Entries.Select(e => (e.Author, e.Text, e.Type)).Should().Equal(
            ("Researcher", "Hello", AgentUpdateTypes.Message),
            ("Researcher", "{}", AgentUpdateTypes.ToolCall));
    }
}
