using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Workflows;
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

        _context.Response.ContentType.Should().Be("text/event-stream");
        _context.Response.Headers.CacheControl.ToString().Should().Be("no-cache");
        Output().Should().StartWith(":");
        DataFrames().Should().BeEmpty();
    }

    [Fact]
    public async Task WriteUpdateAsync_WritesTheAssistantFrameShape()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sse = await StartAsync();

        await sse.WriteUpdateAsync(AgentUpdateTypes.Message, "Hello", ct);

        var frame = DataFrames().Should().ContainSingle().Subject;
        frame.GetProperty("type").GetString().Should().Be(AgentUpdateTypes.Message);
        frame.GetProperty("message").GetProperty("content").GetString().Should().Be("Hello");
        frame.GetProperty("done").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_WritesOneDoneFrame_AndDropsEverythingAfterIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sse = await StartAsync();

        await sse.CompleteAsync(ct);
        await sse.FailAsync("too late");
        await sse.WriteUpdateAsync(AgentUpdateTypes.Message, "too late", ct);

        var frame = DataFrames().Should().ContainSingle().Subject;
        frame.GetProperty("type").GetString().Should().Be(AgentUpdateTypes.Done);
        frame.GetProperty("done").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task FailAsync_WritesAnErrorFrame()
    {
        await using var sse = await StartAsync();

        await sse.FailAsync("boom");

        var frame = DataFrames().Should().ContainSingle().Subject;
        frame.GetProperty("type").GetString().Should().Be(AgentUpdateTypes.Error);
        frame.GetProperty("message").GetString().Should().Be("boom");
        frame.GetProperty("done").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task FailAsync_SwallowsWriteFailures()
    {
        await using var sse = await StartAsync();
        await _body.DisposeAsync();

        var fail = () => sse.FailAsync("boom");

        await fail.Should().NotThrowAsync();
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
        output.Should().Contain("id: 1\ndata: ").And.Contain("id: 3\ndata: ");
        output.Should().NotContain("id: 2").And.NotContain("id: 4");

        var frames = DataFrames();
        frames.Should().HaveCount(2);
        frames[0].GetProperty("Author").GetString().Should().Be("Researcher");
        frames[0].GetProperty("Text").GetString().Should().Be("hi");
        frames[1].GetProperty("type").GetString().Should().Be(AgentUpdateTypes.Done);
    }

    [Theory]
    [InlineData(AgentUpdateTypes.Error, "error")]
    [InlineData(AgentUpdateTypes.Cancelled, "cancelled")]
    public async Task ForwardAsync_MapsTerminalRelayUpdatesToTerminalFrames(string updateType, string expectedFrameType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var sse = await StartAsync();

        await sse.ForwardAsync(Frames(new StreamFrame(7, new AgentStreamUpdate("System", "Run failed.", updateType))), ct);

        var frame = DataFrames().Should().ContainSingle().Subject;
        frame.GetProperty("type").GetString().Should().Be(expectedFrameType);
        frame.GetProperty("done").GetBoolean().Should().BeTrue();
        Output().Should().Contain("id: 7\n");
    }

    [Fact]
    public async Task Heartbeat_WritesCommentsUntilDisposed()
    {
        var sse = await SseStreamWriter.StartAsync(_context.Response, TimeSpan.FromMilliseconds(10), NullLogger.Instance, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        await sse.DisposeAsync();

        Output().Should().Contain(": ping\n\n");
    }

    private Task<SseStreamWriter> StartAsync() =>
        SseStreamWriter.StartAsync(_context.Response, NoHeartbeat, NullLogger.Instance, CancellationToken.None);

    private string Output() => Encoding.UTF8.GetString(_body.ToArray());

    private List<JsonElement> DataFrames() =>
        [.. Output()
            .Split('\n')
            .Where(line => line.StartsWith("data: ", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line["data: ".Length..]).RootElement.Clone())];

    private static async IAsyncEnumerable<StreamFrame> Frames(params StreamFrame[] frames)
    {
        foreach (var frame in frames)
        {
            await Task.Yield();
            yield return frame;
        }
    }
}
