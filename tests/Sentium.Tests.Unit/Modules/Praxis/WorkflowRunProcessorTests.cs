using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Titles;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

public sealed class WorkflowRunProcessorTests : IAsyncDisposable
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    private readonly WorkflowRunQueue _queue = new();
    private readonly StreamRelay _relay = new(new ConfigurationBuilder().Build(), NullLogger<StreamRelay>.Instance);
    private readonly WorkflowExecutionRegistry _registry = new();
    private readonly PdpContextAccessor _runContext = new();
    private readonly IOrchestrator _orchestrator = Substitute.For<IOrchestrator>();
    private readonly IWorkflowRunRepository _runs = Substitute.For<IWorkflowRunRepository>();
    private readonly ITitleGenerator _titles = Substitute.For<ITitleGenerator>();
    private ServiceProvider? _services;
    private WorkflowRunProcessor? _processor;

    [Fact]
    public async Task CompletedRun_IsPersisted_AndEndsWithDone()
    {
        var ct = TestContext.Current.CancellationToken;
        _orchestrator.RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>()).Returns(new WorkflowResult { Explanation = "ok" });
        await StartAsync(ct);

        var trigger = await EnqueueAsync(ct);
        var frames = await ReadUntilTerminalAsync(trigger.StreamId, ct);

        frames[^1].Type.Should().Be(AgentUpdateTypes.Done);
        await _runs.Received(1).AddAsync(Arg.Is<WorkflowRun>(r => r.Explanation == "ok" && r.TriggerPayload == trigger.Payload), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletedRun_IsTitled_FromItsScenario()
    {
        var ct = TestContext.Current.CancellationToken;
        _orchestrator.RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>()).Returns(new WorkflowResult { Explanation = "ok" });
        _titles.GenerateAsync("test", Arg.Any<CancellationToken>()).Returns("Generated title");
        await StartAsync(ct);

        var trigger = await EnqueueAsync(ct);
        await ReadUntilTerminalAsync(trigger.StreamId, ct);

        await _runs.Received(1).AddAsync(Arg.Is<WorkflowRun>(r => r.Title == "Generated title"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunWithoutScenario_IsNotTitled()
    {
        var ct = TestContext.Current.CancellationToken;
        _orchestrator.RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>()).Returns(new WorkflowResult { Explanation = "ok" });
        await StartAsync(ct);

        var trigger = await EnqueueAsync(ct, payload: "{\"something\":\"else\"}");
        await ReadUntilTerminalAsync(trigger.StreamId, ct);

        await _runs.Received(1).AddAsync(Arg.Is<WorkflowRun>(r => r.Title == null), Arg.Any<CancellationToken>());
        await _titles.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ExposesActingUserAndCorrelation_ToTheWorkflow()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        Guid? seenUser = null;
        string? seenCorrelation = null;
        string? seenPrompt = null;

        _orchestrator.RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                seenUser = _runContext.UserId;
                seenCorrelation = _runContext.CorrelationId;
                seenPrompt = _runContext.OriginalUserPrompt;
                return Task.FromResult(new WorkflowResult { Explanation = "ok" });
            });
        await StartAsync(ct);

        var trigger = await EnqueueAsync(ct, userId);
        await ReadUntilTerminalAsync(trigger.StreamId, ct);

        seenUser.Should().Be(userId);
        seenCorrelation.Should().Be(trigger.StreamId);
        seenPrompt.Should().Be("test");
    }

    [Fact]
    public async Task FailedAttempt_IsRetried()
    {
        var ct = TestContext.Current.CancellationToken;
        _orchestrator.RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("model offline"), _ => Task.FromResult(new WorkflowResult { Explanation = "ok" }));
        await StartAsync(ct);

        var trigger = await EnqueueAsync(ct);
        var frames = await ReadUntilTerminalAsync(trigger.StreamId, ct);

        frames.Should().Contain(f => f.Type == AgentUpdateTypes.Status && f.Text.StartsWith("Attempt 1 failed", StringComparison.Ordinal));
        frames[^1].Type.Should().Be(AgentUpdateTypes.Done);
        await _orchestrator.Received(2).RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserCancel_StopsRun_AndEndsWithCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        HangUntilCancelled();
        await StartAsync(ct);

        var trigger = await EnqueueAsync(ct);
        await WaitUntilAsync(() => _registry.Cancel(trigger.StreamId), ct);
        var frames = await ReadUntilTerminalAsync(trigger.StreamId, ct);

        frames[^1].Type.Should().Be(AgentUpdateTypes.Cancelled);
        await _runs.Received(1).AddAsync(Arg.Is<WorkflowRun>(r => r.Explanation.StartsWith("[CANCELLED] Stopped by user") && r.Title == "test"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Timeout_EndsWithError()
    {
        var ct = TestContext.Current.CancellationToken;
        HangUntilCancelled();
        await StartAsync(ct, ("Orchestration:WorkflowTimeout", "00:00:00.2"));

        var trigger = await EnqueueAsync(ct);
        var frames = await ReadUntilTerminalAsync(trigger.StreamId, ct);

        frames[^1].Type.Should().Be(AgentUpdateTypes.Error);
        await _orchestrator.Received(1).RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>());
        await _runs.Received(1).AddAsync(Arg.Is<WorkflowRun>(r => r.Explanation.StartsWith("[TIMEOUT]")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Shutdown_RecordsRunningAndQueuedRuns_AndEndsTheirStreams()
    {
        var ct = TestContext.Current.CancellationToken;
        HangUntilCancelled();
        await StartAsync(ct, ("Orchestration:MaxConcurrentWorkflows", "1"));

        var running = await EnqueueAsync(ct);
        var queued = await EnqueueAsync(ct);
        await WaitUntilAsync(() => _orchestrator.ReceivedCalls().Any(), ct);

        await _processor!.StopAsync(ct);

        (await ReadUntilTerminalAsync(running.StreamId, ct))[^1].Type.Should().Be(AgentUpdateTypes.Error);
        (await ReadUntilTerminalAsync(queued.StreamId, ct))[^1].Type.Should().Be(AgentUpdateTypes.Error);
        await _runs.Received(1).AddAsync(Arg.Is<WorkflowRun>(r => r.Explanation.StartsWith("[CANCELLED] Interrupted by application shutdown")), Arg.Any<CancellationToken>());
        await _runs.Received(1).AddAsync(Arg.Is<WorkflowRun>(r => r.Explanation.StartsWith("[CANCELLED] Not started")), Arg.Any<CancellationToken>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor is not null)
        {
            await _processor.StopAsync(CancellationToken.None);
            _processor.Dispose();
        }

        if (_services is not null)
        {
            await _services.DisposeAsync();
        }

        _relay.Dispose();
    }

    private void HangUntilCancelled()
        => _orchestrator.RunAsync(Arg.Any<WorkflowTrigger>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Delay(Timeout.Infinite, call.ArgAt<CancellationToken>(1));
                return new WorkflowResult();
            });

    private async Task StartAsync(CancellationToken ct, params (string Key, string Value)[] settings)
    {
        var services = new ServiceCollection();
        services.AddScoped<SystemScopeContext>();
        services.AddSingleton(_orchestrator);
        services.AddSingleton(_runs);
        services.AddSingleton(_titles);
        _services = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        _processor = new WorkflowRunProcessor(
            _queue,
            _relay,
            _services.GetRequiredService<IServiceScopeFactory>(),
            _registry,
            _runContext,
            configuration,
            NullLogger<WorkflowRunProcessor>.Instance);

        await _processor.StartAsync(ct);
    }

    private async Task<WorkflowTrigger> EnqueueAsync(CancellationToken ct, Guid? userId = null, string payload = "{\"activity\":\"test\"}")
    {
        var trigger = new WorkflowTrigger
        {
            TriggerType = WorkflowTriggerTypes.Dynamic,
            Payload = payload,
            UserId = userId,
            StreamId = Guid.NewGuid().ToString("N")
        };

        await _queue.EnqueueAsync(trigger, ct);
        return trigger;
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

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(WaitLimit);

        while (!condition())
        {
            await Task.Delay(20, cts.Token);
        }
    }
}
