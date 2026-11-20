using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Praxis.Common.Streaming;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Streaming;

/// <summary>
/// The SSE transport shared by the assistant and workflow streams: headers, frame shapes, relay replay ids, the
/// heartbeat, and the rule that a stream ends with at most one terminal frame.
/// </summary>
public sealed class SseStreamWriterTests
{
    private static readonly TimeSpan NoHeartbeat = TimeSpan.FromHours(1);

    private readonly DefaultHttpContext _context = new();
    private readonly MemoryStream _body = new();

    public SseStreamWriterTests()
    {
        _context.Response.Body = _body;
    }

    [Fact]
    public async Task StartAsync_SetsEventStreamHeaders_AndOpensWithAComment()
    {
        await using var sse = await StartAsync();

        Assert.Equal("text/event-stream", _context.Response.ContentType);
        Assert.Equal("no-cache", _context.Response.Headers.CacheControl.ToString());
        Assert.StartsWith(":", Output(), StringComparison.Ordinal);
        Assert.Empty(DataFrames());
    }

    [Fact]
    public async Task WriteUpdateAsync_WritesTheAssistantFrameShape()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sse = await StartAsync();

        await sse.WriteUpdateAsync(AgentUpdateTypes.Message, "Hello", ct);

        var frame = Assert.Single(DataFrames());
        Assert.Equal(AgentUpdateTypes.Message, frame.GetProperty("type").GetString());
        Assert.Equal("Hello", frame.GetProperty("message").GetProperty("content").GetString());
        Assert.False(frame.GetProperty("done").GetBoolean());
    }

    [Fact]
    public async Task CompleteAsync_WritesOneDoneFrame_AndDropsEverythingAfterIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sse = await StartAsync();

        await sse.CompleteAsync(ct);
        await sse.FailAsync("too late");
        await sse.WriteUpdateAsync(AgentUpdateTypes.Message, "too late", ct);

        var frame = Assert.Single(DataFrames());
        Assert.Equal(AgentUpdateTypes.Done, frame.GetProperty("type").GetString());
        Assert.True(frame.GetProperty("done").GetBoolean());
    }

    [Fact]
    public async Task FailAsync_WritesAnErrorFrame()
    {
        await using var sse = await StartAsync();

        await sse.FailAsync("boom");

        var frame = Assert.Single(DataFrames());
        Assert.Equal(AgentUpdateTypes.Error, frame.GetProperty("type").GetString());
        Assert.Equal("boom", frame.GetProperty("message").GetString());
        Assert.True(frame.GetProperty("done").GetBoolean());
    }

    [Fact]
    public async Task FailAsync_SwallowsWriteFailures()
    {
        await using var sse = await StartAsync();
        await _body.DisposeAsync();

        var fail = () => sse.FailAsync("boom");

        await fail();
    }

    [Fact]
    public async Task ForwardAsync_WritesRelayFramesWithIds_SkipsEmptyText_AndStopsAtTheTerminalFrame()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sse = await StartAsync();

        await sse.ForwardAsync(
            Frames(
                new StreamFrame(1, new AgentStreamUpdate("Researcher", "hi")),
                new StreamFrame(2, new AgentStreamUpdate("Researcher", string.Empty)),
                new StreamFrame(3, new AgentStreamUpdate("System", string.Empty, AgentUpdateTypes.Done)),
                new StreamFrame(4, new AgentStreamUpdate("Researcher", "late"))),
            ct);

        var output = Output();
        Assert.Contains("id: 1\ndata: ", output, StringComparison.Ordinal);
        Assert.Contains("id: 3\ndata: ", output, StringComparison.Ordinal);
        Assert.DoesNotContain("id: 2", output, StringComparison.Ordinal);
        Assert.DoesNotContain("id: 4", output, StringComparison.Ordinal);

        var frames = DataFrames();
        Assert.Equal(2, frames.Count);
        Assert.Equal("Researcher", frames[0].GetProperty("Author").GetString());
        Assert.Equal("hi", frames[0].GetProperty("Text").GetString());
        Assert.Equal(AgentUpdateTypes.Done, frames[1].GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(AgentUpdateTypes.Error, "error")]
    [InlineData(AgentUpdateTypes.Cancelled, "cancelled")]
    public async Task ForwardAsync_MapsTerminalRelayUpdatesToTerminalFrames(string updateType, string expectedFrameType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sse = await StartAsync();

        await sse.ForwardAsync(Frames(new StreamFrame(7, new AgentStreamUpdate("System", "Run failed.", updateType))), ct);

        var frame = Assert.Single(DataFrames());
        Assert.Equal(expectedFrameType, frame.GetProperty("type").GetString());
        Assert.True(frame.GetProperty("done").GetBoolean());
        Assert.Contains("id: 7\n", Output(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Heartbeat_WritesCommentsUntilDisposed()
    {
        var sse = await SseStreamWriter.StartAsync(_context.Response, TimeSpan.FromMilliseconds(10), NullLogger.Instance, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        await sse.DisposeAsync();

        Assert.Contains(": ping\n\n", Output(), StringComparison.Ordinal);
    }

    private Task<SseStreamWriter> StartAsync() =>
        SseStreamWriter.StartAsync(_context.Response, NoHeartbeat, NullLogger.Instance, CancellationToken.None);

    private string Output() => Encoding.UTF8.GetString(_body.ToArray());

    private List<JsonElement> DataFrames()
    {
        return [.. Output()
            .Split('\n')
            .Where(line => line.StartsWith("data: ", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line["data: ".Length..]).RootElement.Clone())];
    }

    private static async IAsyncEnumerable<StreamFrame> Frames(params StreamFrame[] frames)
    {
        foreach (var frame in frames)
        {
            await Task.Yield();
            yield return frame;
        }
    }
}
