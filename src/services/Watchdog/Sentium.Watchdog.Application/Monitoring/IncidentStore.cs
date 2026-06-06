using System.Collections.Concurrent;
using Sentium.Watchdog.Core.Monitoring;

namespace Sentium.Watchdog.Application.Monitoring;

public sealed class IncidentStore : IIncidentStore
{
    private const int MaxResolvedHistory = 100;

    private readonly ConcurrentDictionary<string, Incident> _open = new();
    private readonly LinkedList<Incident> _resolved = new();
    private readonly Lock _resolvedGate = new();

    public Incident? GetOpen(string target) => _open.GetValueOrDefault(target);

    public Incident? Open(string target, ComponentKind kind, IncidentSeverity severity, ServiceStatus observedStatus, string? description)
    {
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            Target = target,
            Kind = kind,
            Severity = severity,
            Status = IncidentStatus.Open,
            OpenedAt = DateTimeOffset.UtcNow,
            Description = description,
            LastObservedStatus = observedStatus
        };

        return _open.TryAdd(target, incident) ? incident : null;
    }

    public Incident? Resolve(string target)
    {
        if (!_open.TryRemove(target, out var open))
        {
            return null;
        }

        var resolvedAt = DateTimeOffset.UtcNow;
        var resolved = open with
        {
            Status = IncidentStatus.Resolved,
            ResolvedAt = resolvedAt,
            DurationMs = (resolvedAt - open.OpenedAt).TotalMilliseconds
        };

        lock (_resolvedGate)
        {
            _resolved.AddFirst(resolved);
            while (_resolved.Count > MaxResolvedHistory)
            {
                _resolved.RemoveLast();
            }
        }

        return resolved;
    }

    public IReadOnlyList<Incident> GetAll()
    {
        var open = _open.Values.OrderByDescending(i => i.OpenedAt);

        lock (_resolvedGate)
        {
            return [.. open, .. _resolved];
        }
    }

    public int OpenCount => _open.Count;
}
