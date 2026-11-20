using Microsoft.Extensions.Hosting;
using Sentium.Scheduling.Features.BackgroundJobs;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Scheduling;

public sealed class BackgroundServiceInspectorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Describes_RunningAndFaultedWorkers_ByTheirLoopTask()
    {
        using var running = new LoopingWorker();
        using var faulted = new FaultingWorker();
        await running.StartAsync(Ct);
        await faulted.StartAsync(Ct);
        await WaitUntilCompletedAsync(faulted.ExecuteTask!);

        var described = BackgroundServiceInspector.Describe([running, faulted]);

        var loop = Assert.Single(described, s => s.Type == typeof(LoopingWorker).FullName);
        Assert.Equal("Running", loop.State);
        Assert.Equal("Looping worker", loop.Name);
        Assert.Equal("Tests", loop.Module);

        var broken = Assert.Single(described, s => s.Type == typeof(FaultingWorker).FullName);
        Assert.Equal("Faulted", broken.State);
        Assert.Equal("disk gone", broken.Error);

        await running.StopAsync(Ct);
    }

    [Fact]
    public void NotYetStartedWorker_IsReportedAsSuch()
    {
        using var worker = new LoopingWorker();

        Assert.Equal("NotStarted", Assert.Single(BackgroundServiceInspector.Describe([worker])).State);
    }

    private static async Task WaitUntilCompletedAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed class LoopingWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private sealed class FaultingWorker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            throw new InvalidOperationException("disk gone");
        }
    }
}
