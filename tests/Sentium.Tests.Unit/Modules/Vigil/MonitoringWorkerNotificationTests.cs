using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Nuntius.Contracts;
using Sentium.Vigil.Features.Monitoring;
using Sentium.Vigil.Features.Settings;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil;

public sealed class MonitoringWorkerNotificationTests
{
    private const string Target = "Sandbox";

    private readonly INotifier _notifier = Substitute.For<INotifier>();
    private readonly List<(NotificationRecipient Recipient, NotificationMessage Message)> _sent = [];

    public MonitoringWorkerNotificationTests()
    {
        _notifier
            .NotifyAsync(Arg.Any<NotificationRecipient>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(call =>
            {
                lock (_sent)
                {
                    _sent.Add((call.ArgAt<NotificationRecipient>(0), call.ArgAt<NotificationMessage>(1)));
                }
            });
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private int SentCount
    {
        get
        {
            lock (_sent)
            {
                return _sent.Count;
            }
        }
    }

    [Fact]
    public async Task IncidentOpenedAndResolved_NotifySovereigns_WithSeparateDedupeKeys()
    {
        var probe = Substitute.For<IHealthProbe>();
        probe.Target.Returns(Target);
        probe.Kind.Returns(ComponentKind.Service);
        probe.ProbeAsync(Arg.Any<CancellationToken>()).Returns(Status(ServiceStatus.Unhealthy), Status(ServiceStatus.Healthy));

        var settings = Substitute.For<IVigilSettingsProvider>();
        settings.GetAsync(Arg.Any<CancellationToken>()).Returns(new VigilRuntimeSettings { PollIntervalSeconds = 0 });

        using var worker = new MonitoringWorker(
            [probe], new ServiceHealthStateStore(), new IncidentStore(), settings, Substitute.For<IVigilFeed>(), _notifier, NullLogger<MonitoringWorker>.Instance);

        await worker.StartAsync(Ct);
        await WaitUntilAsync(() => SentCount >= 2);
        await worker.StopAsync(Ct);

        List<(NotificationRecipient Recipient, NotificationMessage Message)> sent;
        lock (_sent)
        {
            sent = [.. _sent];
        }

        Assert.Equal(2, sent.Count);
        Assert.All(sent, s => Assert.True(s.Recipient == NotificationRecipient.Sovereigns && s.Message.Category == NotificationCategories.SystemIncident));

        var (_, opened) = sent[0];
        Assert.Equal(NotificationSeverity.Critical, opened.Severity);
        Assert.Contains(Target, opened.Title, StringComparison.Ordinal);
        Assert.Equal("/vigil", opened.Link);

        var (_, resolved) = sent[1];
        Assert.Equal(NotificationSeverity.Info, resolved.Severity);
        Assert.Contains(Target, resolved.Title, StringComparison.Ordinal);
        // a recovery must not overwrite the unread outage
        Assert.NotEqual(opened.DedupeKey, resolved.DedupeKey);
    }

    [Fact]
    public async Task ProbeWithItsOwnCategory_NotifiesUnderThatCategory()
    {
        var probe = Substitute.For<IIncidentCategoryProbe>();
        probe.Target.Returns(Target);
        probe.Kind.Returns(ComponentKind.Infrastructure);
        probe.NotificationCategory.Returns(NotificationCategories.SystemBackup);
        probe.ProbeAsync(Arg.Any<CancellationToken>()).Returns(Status(ServiceStatus.Unhealthy), Status(ServiceStatus.Healthy));

        var settings = Substitute.For<IVigilSettingsProvider>();
        settings.GetAsync(Arg.Any<CancellationToken>()).Returns(new VigilRuntimeSettings { PollIntervalSeconds = 0 });

        using var worker = new MonitoringWorker(
            [probe], new ServiceHealthStateStore(), new IncidentStore(), settings, Substitute.For<IVigilFeed>(), _notifier, NullLogger<MonitoringWorker>.Instance);

        await worker.StartAsync(Ct);
        await WaitUntilAsync(() => SentCount >= 2);
        await worker.StopAsync(Ct);

        lock (_sent)
        {
            Assert.Equal(2, _sent.Count);
            Assert.All(_sent, s => Assert.True(s.Message.Category == NotificationCategories.SystemBackup
                && s.Message.DedupeKey!.StartsWith(NotificationCategories.SystemBackup + ":", StringComparison.Ordinal)));
        }
    }

    private static ServiceHealthStatus Status(ServiceStatus status)
        => new() { ServiceName = Target, Status = status, LatencyMs = 1, CheckedAt = DateTimeOffset.UtcNow, Details = "probe result" };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, Ct);
        }
    }
}
