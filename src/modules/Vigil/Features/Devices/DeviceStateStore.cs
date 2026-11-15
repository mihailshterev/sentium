using System.Collections.Concurrent;

namespace Sentium.Vigil.Features.Devices;

internal sealed class DeviceStateStore(TimeProvider timeProvider) : IDeviceStateStore
{
    public const int MissedReportsBeforeOffline = 3;

    private const int MaxSamples = 120;

    /// <summary>
    /// Slack for a Report that is in flight when its interval ends.
    /// </summary>
    private static readonly TimeSpan ReportGrace = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<Guid, DeviceState> _devices = new();

    public DeviceRecordResult Record(Guid deviceId, Guid ownerUserId, DeviceReport report, int reportIntervalSeconds)
    {
        ArgumentNullException.ThrowIfNull(report);

        var now = timeProvider.GetUtcNow();
        var state = _devices.GetOrAdd(deviceId, _ => new DeviceState(ownerUserId));

        lock (state.Gate)
        {
            state.Report = report;
            state.ReportedAt = now;
            state.OfflineAfter = (TimeSpan.FromSeconds(reportIntervalSeconds) * MissedReportsBeforeOffline) + ReportGrace;
            state.OfflineAnnounced = false;

            state.Samples.Enqueue(new DeviceSample(now, report.CpuPercent, MemoryUsedPercent(report)));
            while (state.Samples.Count > MaxSamples)
            {
                state.Samples.Dequeue();
            }

            var newlyFullDisks = state.DiskRule.Evaluate(report.Disks);

            return new DeviceRecordResult(new DeviceSnapshot(deviceId, ownerUserId, report, now, IsOnline: true), newlyFullDisks);
        }
    }

    public DeviceSnapshot? Get(Guid deviceId)
    {
        if (!_devices.TryGetValue(deviceId, out var state))
        {
            return null;
        }

        lock (state.Gate)
        {
            return Snapshot(deviceId, state, timeProvider.GetUtcNow());
        }
    }

    public IReadOnlyList<DeviceSample> GetSamples(Guid deviceId, int take)
    {
        if (!_devices.TryGetValue(deviceId, out var state))
        {
            return [];
        }

        lock (state.Gate)
        {
            var count = Math.Min(Math.Max(take, 0), state.Samples.Count);
            return count == 0 ? [] : [.. state.Samples.TakeLast(count)];
        }
    }

    public IReadOnlyList<DeviceSnapshot> TakeNewlyOffline()
    {
        var now = timeProvider.GetUtcNow();
        List<DeviceSnapshot> newlyOffline = [];

        foreach (var (deviceId, state) in _devices)
        {
            lock (state.Gate)
            {
                var snapshot = Snapshot(deviceId, state, now);
                if (snapshot is { IsOnline: false } && !state.OfflineAnnounced)
                {
                    state.OfflineAnnounced = true;
                    newlyOffline.Add(snapshot);
                }
            }
        }

        return newlyOffline;
    }

    public bool Remove(Guid deviceId) => _devices.TryRemove(deviceId, out _);

    private static DeviceSnapshot? Snapshot(Guid deviceId, DeviceState state, DateTimeOffset now)
    {
        if (state.Report is null)
        {
            return null;
        }

        var isOnline = now - state.ReportedAt <= state.OfflineAfter;
        return new DeviceSnapshot(deviceId, state.OwnerUserId, state.Report, state.ReportedAt, isOnline);
    }

    private static double MemoryUsedPercent(DeviceReport report)
    {
        return report.MemoryTotalBytes <= 0
            ? 0
            : (report.MemoryTotalBytes - report.MemoryAvailableBytes) * 100d / report.MemoryTotalBytes;
    }

    private sealed class DeviceState(Guid ownerUserId)
    {
        public Lock Gate { get; } = new();
        public Guid OwnerUserId { get; } = ownerUserId;
        public DeviceReport? Report { get; set; }
        public DateTimeOffset ReportedAt { get; set; }
        public TimeSpan OfflineAfter { get; set; }
        public bool OfflineAnnounced { get; set; }
        public Queue<DeviceSample> Samples { get; } = new();
        public DiskFullRule DiskRule { get; } = new();
    }
}
