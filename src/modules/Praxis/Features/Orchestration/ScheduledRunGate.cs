using System.Collections.Concurrent;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// The Schedules whose run is queued or running. A fire that arrives while its Schedule holds a slot is skipped. Runs do
/// not survive a restart, so neither do the slots.
/// </summary>
internal sealed class ScheduledRunGate
{
    private readonly ConcurrentDictionary<string, byte> _active = new(StringComparer.Ordinal);

    public bool TryAcquire(string scheduleId) => _active.TryAdd(scheduleId, 0);

    public void Release(string scheduleId) => _active.TryRemove(scheduleId, out _);
}
