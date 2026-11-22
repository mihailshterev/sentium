using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Orchestration;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Orchestration;

/// <summary>
/// The SSE view of a relay stream: frame shapes, sequence ids for resuming, and exactly one terminal frame.
/// </summary>
public sealed class RelayStreamsTests : IDisposable
{
    private const string StreamId = "stream-1";

    private static readonly TimeSpan NoHeartbeat = TimeSpan.FromHours(1);
    private static readonly TimeSpan NoCap = TimeSpan.FromHours(1);

    private readonly StreamRelay _relay = new(Options.Create(new StreamingOptions { TextCoalesceWindow = TimeSpan.Zero }), NullLogger<StreamRelay>.Instance);
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().BuildServiceProvider();

    [Fact]
    public async Task Stream_WritesUpdatesWithTheirSequenceIds_AndEndsWithOneDoneFrame()
    {
        _relay.Publish(StreamId, new AgentStreamUpdate("Planner", "Hello", AgentUpdateTypes.Message));
        _relay.Publish(StreamId, new AgentStreamUpdate("System", AgentUpdateTypes.Done, AgentUpdateTypes.Done));
        _relay.Publish(StreamId, new AgentStreamUpdate("Planner", "too late", AgentUpdateTypes.Message));

        var events = await StreamAsync(fromSeq: 0);

        Assert.Equal(2, events.Count);
        Assert.Equal("1", events[0].Id);
        Assert.Equal("Planner", events[0].Data.GetProperty("author").GetString());
        Assert.Equal("Hello", events[0].Data.GetProperty("text").GetString());
        Assert.Equal(AgentUpdateTypes.Message, events[0].Data.GetProperty("type").GetString());
        Assert.Equal(AgentUpdateTypes.Done, events[1].Data.GetProperty("type").GetString());
        Assert.True(events[1].Data.GetProperty("done").GetBoolean());
    }

    [Fact]
    public async Task Stream_ResumesAfterTheGivenSequence()
    {
        _relay.Publish(StreamId, new AgentStreamUpdate("A", "one"));
        _relay.Publish(StreamId, new AgentStreamUpdate("A", "two"));
        _relay.Publish(StreamId, new AgentStreamUpdate("System", "boom", AgentUpdateTypes.Error));

        var events = await StreamAsync(fromSeq: 1);

        Assert.Equal("two", events[0].Data.GetProperty("text").GetString());
        Assert.Equal(AgentUpdateTypes.Error, events[1].Data.GetProperty("type").GetString());
        Assert.Equal("boom", events[1].Data.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnknownStream_GetsOneGoneFrame()
    {
        var events = await StreamAsync(fromSeq: 0, streamId: "never-started");

        var gone = Assert.Single(events);
        Assert.Equal(AgentUpdateTypes.Gone, gone.Data.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("7", null, 7)]
    [InlineData(null, "5", 5)]
    [InlineData("9", "5", 9)]
    [InlineData(null, "nope", 0)]
    [InlineData(null, null, 0)]
    public void ResumePoint_PrefersTheLastEventIdHeader_OverTheQuery(string? header, string? after, long expected)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers["Last-Event-ID"] = header;
        }

        if (after is not null)
        {
            context.Request.QueryString = new QueryString($"?after={after}");
        }

        Assert.Equal(expected, RelayStreams.ResumePoint(context.Request));
    }

    public void Dispose()
    {
        _relay.Dispose();
        _services.Dispose();
    }

    private async Task<List<(string? Id, JsonElement Data)>> StreamAsync(long fromSeq, string streamId = StreamId)
    {
        var ct = TestContext.Current.CancellationToken;
        var context = new DefaultHttpContext { RequestServices = _services };
        using var body = new MemoryStream();
        context.Response.Body = body;

        await RelayStreams.Create(_relay, streamId, fromSeq, NoHeartbeat, NoCap, ct).ExecuteAsync(context);

        return Parse(Encoding.UTF8.GetString(body.ToArray()));
    }

    private static List<(string? Id, JsonElement Data)> Parse(string output)
    {
        var events = new List<(string? Id, JsonElement Data)>();
        foreach (var block in output.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = block.Split('\n');
            var data = lines.FirstOrDefault(l => l.StartsWith("data: ", StringComparison.Ordinal));
            if (data is null)
            {
                continue;
            }

            var id = lines.FirstOrDefault(l => l.StartsWith("id: ", StringComparison.Ordinal))?["id: ".Length..];
            events.Add((id, JsonDocument.Parse(data["data: ".Length..]).RootElement.Clone()));
        }

        return events;
    }
}
