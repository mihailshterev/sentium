using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Orchestration;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Orchestration;

public sealed class StreamRelayTests : IDisposable
{
    private const string Author = "Assistant";

    private readonly StreamRelay _relay = new(
        Options.Create(new StreamingOptions { TextCoalesceWindow = TimeSpan.FromMinutes(1) }),
        NullLogger<StreamRelay>.Instance);

    public void Dispose() => _relay.Dispose();

    [Fact]
    public async Task ConsecutiveTextTokens_AreJoinedIntoOneFrame_ReleasedByTheNextOtherFrame()
    {
        var streamId = NewStreamId();
        _relay.Publish(streamId, Update("Hel", AgentUpdateTypes.Message));
        _relay.Publish(streamId, Update("lo", AgentUpdateTypes.Message));
        _relay.Publish(streamId, Update("""{"callId":"1"}""", AgentUpdateTypes.ToolCall));
        _relay.Publish(streamId, Update(" world", AgentUpdateTypes.Message));
        _relay.Publish(streamId, Update(AgentUpdateTypes.Done, AgentUpdateTypes.Done));

        var frames = await ReadAllAsync(streamId, 0);

        Assert.Equal(
            [(AgentUpdateTypes.Message, "Hello"), (AgentUpdateTypes.ToolCall, """{"callId":"1"}"""), (AgentUpdateTypes.Message, " world"), (AgentUpdateTypes.Done, AgentUpdateTypes.Done)],
            frames.Select(f => (f.Update.Type, f.Update.Text)));
        Assert.Equal([1L, 2, 3, 4], frames.Select(f => f.Seq));
    }

    [Fact]
    public async Task TextOfDifferentTypesOrAuthors_IsNeverJoined()
    {
        var streamId = NewStreamId();
        _relay.Publish(streamId, Update("think", AgentUpdateTypes.Thought));
        _relay.Publish(streamId, Update("say", AgentUpdateTypes.Message));
        _relay.Publish(streamId, new AgentStreamUpdate("Planner", "plan", AgentUpdateTypes.Message));
        _relay.Publish(streamId, Update(AgentUpdateTypes.Done, AgentUpdateTypes.Done));

        var frames = await ReadAllAsync(streamId, 0);

        Assert.Equal(["think", "say", "plan", AgentUpdateTypes.Done], frames.Select(f => f.Update.Text));
    }

    [Fact]
    public async Task HeldText_IsReleasedAfterTheWindow_WithoutAnotherFrame()
    {
        using var relay = new StreamRelay(
            Options.Create(new StreamingOptions { TextCoalesceWindow = TimeSpan.FromMilliseconds(20) }),
            NullLogger<StreamRelay>.Instance);
        var streamId = NewStreamId();
        relay.Claim(streamId, Guid.NewGuid());

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        var reading = relay.Subscribe(streamId, 0, cts.Token).GetAsyncEnumerator(cts.Token);

        relay.Publish(streamId, Update("a", AgentUpdateTypes.Message));
        relay.Publish(streamId, Update("b", AgentUpdateTypes.Message));

        Assert.True(await reading.MoveNextAsync());
        Assert.Equal("ab", reading.Current.Update.Text);
        await reading.DisposeAsync();
    }

    [Fact]
    public async Task ReplayOfABacklogLongerThanTheLiveQueue_LosesNothing()
    {
        using var relay = new StreamRelay(
            Options.Create(new StreamingOptions { TextCoalesceWindow = TimeSpan.Zero }),
            NullLogger<StreamRelay>.Instance);
        var streamId = NewStreamId();

        const int Count = 12_000;
        for (var i = 0; i < Count; i++)
        {
            relay.Publish(streamId, Update("x", AgentUpdateTypes.Message));
        }

        relay.Publish(streamId, Update(AgentUpdateTypes.Done, AgentUpdateTypes.Done));

        var frames = new List<StreamFrame>();
        await foreach (var frame in relay.Subscribe(streamId, 0, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        Assert.Equal(Count + 1, frames.Count);
        Assert.Equal(1, frames[0].Seq);
    }

    [Fact]
    public async Task SubscribingToAnUnknownStream_YieldsNothing_AndDoesNotCreateIt()
    {
        var streamId = NewStreamId();

        var frames = await ReadAllAsync(streamId, 0);

        Assert.Empty(frames);
        Assert.False(_relay.Exists(streamId));
    }

    private async Task<List<StreamFrame>> ReadAllAsync(string streamId, long from)
    {
        var frames = new List<StreamFrame>();
        await foreach (var frame in _relay.Subscribe(streamId, from, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static AgentStreamUpdate Update(string text, string type) => new(Author, text, type);

    private static string NewStreamId() => Guid.NewGuid().ToString("N");
}
