using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Assistant;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Sentinel;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Assistant;

/// <summary>
/// The processor's guarantee: every turn ends its stream with exactly one terminal frame and leaves the registry. The
/// scope here has no <see cref="AssistantTurnRunner"/>, so a turn that starts fails the way a broken run does.
/// </summary>
public sealed class AssistantTurnProcessorTests : IAsyncDisposable
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    private readonly AssistantTurnRegistry _registry = new();
    private readonly AssistantTurnQueue _queue;
    private readonly StreamRelay _relay = new(Options.Create(new StreamingOptions { TextCoalesceWindow = TimeSpan.Zero }), NullLogger<StreamRelay>.Instance);
    private readonly ServiceProvider _services;
    private readonly AssistantTurnProcessor _processor;

    public AssistantTurnProcessorTests()
    {
        _queue = new AssistantTurnQueue(_registry);

        var services = new ServiceCollection();
        services.AddScoped<SystemScopeContext>();
        _services = services.BuildServiceProvider();

        _processor = new AssistantTurnProcessor(
            _queue,
            _registry,
            _relay,
            _services.GetRequiredService<IServiceScopeFactory>(),
            new PdpContextAccessor(),
            Options.Create(new AssistantOptions()),
            NullLogger<AssistantTurnProcessor>.Instance);
    }

    [Fact]
    public async Task FailingTurn_EndsWithOneErrorFrame_AndIsReleased()
    {
        var ct = TestContext.Current.CancellationToken;
        await _processor.StartAsync(ct);

        var turn = Enqueue();
        var frames = await ReadUntilTerminalAsync(turn.TurnId, ct);

        var terminal = Assert.Single(frames);
        Assert.Equal(AgentUpdateTypes.Error, terminal.Type);
        Assert.Equal(CancellationToken.None, _registry.StopToken(turn.TurnId));
    }

    [Fact]
    public async Task TurnStoppedWhileQueued_NeverStarts_AndEndsCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var turn = Enqueue();
        Assert.True(_registry.CancelOwned(turn.TurnId, turn.UserId));

        await _processor.StartAsync(ct);
        var frames = await ReadUntilTerminalAsync(turn.TurnId, ct);

        Assert.Equal(AgentUpdateTypes.Cancelled, Assert.Single(frames).Type);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.StopAsync(CancellationToken.None);
        _processor.Dispose();
        await _services.DisposeAsync();
    }

    private AssistantTurn Enqueue()
    {
        var turn = new AssistantTurn
        {
            TurnId = Guid.NewGuid().ToString("N"),
            UserId = Guid.NewGuid(),
            CorrelationId = "test",
            Model = "test-model",
            Prompt = "hello"
        };

        _relay.Claim(turn.TurnId, turn.UserId);
        _queue.Enqueue(turn);
        return turn;
    }

    private async Task<List<AgentStreamUpdate>> ReadUntilTerminalAsync(string streamId, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(WaitLimit);

        var frames = new List<AgentStreamUpdate>();
        await foreach (var frame in _relay.Subscribe(streamId, 0, cts.Token))
        {
            frames.Add(frame.Update);
        }

        return frames;
    }
}
