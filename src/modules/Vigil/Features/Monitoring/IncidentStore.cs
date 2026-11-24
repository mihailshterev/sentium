using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Sentium.Vigil.Data;
using Sentium.Vigil.Entities;

namespace Sentium.Vigil.Features.Monitoring;

/// <remarks>
/// The monitoring loop decides on the in-memory state and never waits on a failed write: an incident that cannot be
/// written (the database being down is a common cause) stays queued and is written by the next call that reaches the
/// database. The last resolved incidents are also kept in memory, for when the history cannot be read.
/// </remarks>
internal sealed class IncidentStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<IncidentStore> logger) : IIncidentStore
{
    private const int MaxResolvedInMemory = 100;

    private readonly ConcurrentDictionary<string, Incident> _open = new();
    private readonly ConcurrentDictionary<Guid, Incident> _unsaved = new();
    private readonly LinkedList<Incident> _resolved = new();
    private readonly Lock _resolvedGate = new();
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public int OpenCount => _open.Count;

    public Incident? GetOpen(string target) => _open.GetValueOrDefault(target);

    public async Task<Incident?> OpenAsync(
        string target,
        ComponentKind kind,
        IncidentSeverity severity,
        ServiceStatus observedStatus,
        string? description,
        CancellationToken ct = default)
    {
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            Target = target,
            Kind = kind,
            Severity = severity,
            Status = IncidentStatus.Open,
            OpenedAt = timeProvider.GetUtcNow(),
            Description = description,
            LastObservedStatus = observedStatus
        };

        if (!_open.TryAdd(target, incident))
        {
            return null;
        }

        await SaveAsync(incident, ct);
        return incident;
    }

    public async Task<Incident?> ResolveAsync(string target, CancellationToken ct = default)
    {
        if (!_open.TryRemove(target, out var open))
        {
            return null;
        }

        var resolved = Resolved(open, timeProvider.GetUtcNow());
        RememberResolved(resolved);
        await SaveAsync(resolved, ct);
        return resolved;
    }

    public async Task<IReadOnlyList<Incident>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var open = _open.Values.OrderByDescending(i => i.OpenedAt).ToList();
        IReadOnlyList<Incident> resolved;

        try
        {
            await FlushAsync(ct);

            await using var scope = scopeFactory.CreateAsyncScope();
            var rows = await scope.ServiceProvider.GetRequiredService<VigilDbContext>().Incidents
                .AsNoTracking()
                .Where(i => i.ResolvedAt != null)
                .OrderByDescending(i => i.ResolvedAt)
                .Take(limit)
                .ToListAsync(ct);

            resolved = [.. rows.Select(ToIncident)];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The incident history could not be read; showing the incidents since the last start.");

            lock (_resolvedGate)
            {
                resolved = [.. _resolved.Take(limit)];
            }
        }

        return [.. open, .. resolved];
    }

    public async Task RestoreOpenAsync(IReadOnlySet<string> monitoredTargets, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(monitoredTargets);

        await using var scope = scopeFactory.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<VigilDbContext>().Incidents
            .AsNoTracking()
            .Where(i => i.ResolvedAt == null)
            .OrderByDescending(i => i.OpenedAt)
            .ToListAsync(ct);

        var now = timeProvider.GetUtcNow();
        foreach (var incident in rows.Select(ToIncident))
        {
            // Newest first: a second open incident on the same target (or an unmonitored target) can never resolve.
            if (!monitoredTargets.Contains(incident.Target) || !_open.TryAdd(incident.Target, incident))
            {
                _unsaved[incident.Id] = Resolved(incident, now);
            }
        }

        await FlushAsync(ct);

        if (!_open.IsEmpty)
        {
            logger.LogInformation("Restored {Count} incidents that were open when Sentium last stopped.", _open.Count);
        }
    }

    private static Incident Resolved(Incident open, DateTimeOffset resolvedAt)
    {
        return open with
        {
            Status = IncidentStatus.Resolved,
            ResolvedAt = resolvedAt,
            DurationMs = (resolvedAt - open.OpenedAt).TotalMilliseconds
        };
    }

    private static Incident ToIncident(IncidentRecord row)
    {
        return new Incident
        {
            Id = row.Id,
            Target = row.Target,
            Kind = Enum.Parse<ComponentKind>(row.Kind),
            Severity = Enum.Parse<IncidentSeverity>(row.Severity),
            Status = row.ResolvedAt is null ? IncidentStatus.Open : IncidentStatus.Resolved,
            OpenedAt = row.OpenedAt,
            ResolvedAt = row.ResolvedAt,
            DurationMs = row.ResolvedAt is { } resolvedAt ? (resolvedAt - row.OpenedAt).TotalMilliseconds : null,
            Description = row.Description,
            LastObservedStatus = Enum.Parse<ServiceStatus>(row.LastObservedStatus)
        };
    }

    private static void CopyTo(Incident incident, IncidentRecord row)
    {
        row.Target = incident.Target;
        row.Kind = incident.Kind.ToString();
        row.Severity = incident.Severity.ToString();
        row.OpenedAt = incident.OpenedAt;
        row.ResolvedAt = incident.ResolvedAt;
        row.Description = incident.Description;
        row.LastObservedStatus = incident.LastObservedStatus.ToString();
    }

    private void RememberResolved(Incident resolved)
    {
        lock (_resolvedGate)
        {
            _resolved.AddFirst(resolved);
            while (_resolved.Count > MaxResolvedInMemory)
            {
                _resolved.RemoveLast();
            }
        }
    }

    /// <summary>
    /// Queues the incident's latest state and tries to write everything queued. Never throws: the monitoring loop must
    /// keep running whether or not the database is reachable.
    /// </summary>
    private async Task SaveAsync(Incident incident, CancellationToken ct)
    {
        _unsaved[incident.Id] = incident;

        try
        {
            await FlushAsync(ct);
        }
        catch (Exception ex)
        {
            // When shutting down, the queued state is lost; the next start reloads the incident as it was last saved.
            if (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Incident {IncidentId} on {Target} could not be saved yet; it will be retried.", incident.Id, incident.Target);
            }
        }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        if (_unsaved.IsEmpty)
        {
            return;
        }

        await _writeGate.WaitAsync(ct);
        try
        {
            var pending = _unsaved.Values.ToList();
            if (pending.Count == 0)
            {
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<VigilDbContext>();
            var ids = pending.Select(i => i.Id).ToList();
            var rows = await db.Incidents.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

            foreach (var incident in pending)
            {
                if (!rows.TryGetValue(incident.Id, out var row))
                {
                    row = new IncidentRecord { Id = incident.Id };
                    db.Incidents.Add(row);
                    rows[incident.Id] = row;
                }

                CopyTo(incident, row);
            }

            await db.SaveChangesAsync(ct);

            foreach (var incident in pending)
            {
                // Only if unchanged since it was read: a newer state queued meanwhile is written next time.
                _unsaved.TryRemove(new KeyValuePair<Guid, Incident>(incident.Id, incident));
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
