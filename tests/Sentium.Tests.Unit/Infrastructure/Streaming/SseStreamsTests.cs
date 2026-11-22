using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Infrastructure.Streaming;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Streaming;

public sealed class SseStreamsTests : IDisposable
{
    private static readonly TimeSpan NoHeartbeat = TimeSpan.FromHours(1);

    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().BuildServiceProvider();

    [Fact]
    public async Task Create_WritesEachItemsDataAsIs_WithTheEventStreamHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        var (context, body) = NewContext();

        await SseStreams.Create(Items(new SseItem<string>("{\"a\":1}") { EventId = "4" }), NoHeartbeat, ct).ExecuteAsync(context);

        var output = Encoding.UTF8.GetString(body.ToArray());
        Assert.Equal("text/event-stream", context.Response.ContentType);
        Assert.Equal("no", context.Response.Headers["X-Accel-Buffering"].ToString());
        Assert.Contains("data: {\"a\":1}\n", output, StringComparison.Ordinal);
        Assert.Contains("id: 4\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithHeartbeat_SendsPingEvents_WhileTheSourceIsSilent()
    {
        var ct = TestContext.Current.CancellationToken;
        var items = new List<SseItem<string>>();

        await foreach (var item in SseStreams.WithHeartbeat(SlowItem(TimeSpan.FromMilliseconds(300), ct), TimeSpan.FromMilliseconds(40), ct))
        {
            items.Add(item);
        }

        Assert.Contains(items, i => i.EventType == SseStreams.PingEventType);
        Assert.Equal("late", items[^1].Data);
    }

    [Fact]
    public async Task WithHeartbeat_EndsQuietly_WhenTheSourceIsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var items = new List<SseItem<string>>();

        await foreach (var item in SseStreams.WithHeartbeat(CancelledAfterOne(), NoHeartbeat, ct))
        {
            items.Add(item);
        }

        Assert.Equal("first", Assert.Single(items).Data);
    }

    public void Dispose() => _services.Dispose();

    private (DefaultHttpContext Context, MemoryStream Body) NewContext()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = _services };
        context.Response.Body = body;
        return (context, body);
    }

    private static async IAsyncEnumerable<SseItem<string>> Items(params SseItem<string>[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async IAsyncEnumerable<SseItem<string>> SlowItem(TimeSpan delay, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Delay(delay, ct);
        yield return new SseItem<string>("late");
    }

    private static async IAsyncEnumerable<SseItem<string>> CancelledAfterOne()
    {
        yield return new SseItem<string>("first");
        await Task.Yield();
        throw new OperationCanceledException();
    }
}
