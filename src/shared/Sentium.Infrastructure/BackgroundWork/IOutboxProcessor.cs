namespace Sentium.Infrastructure.BackgroundWork;

/// <summary>
/// One outbox's work, resolved in a fresh system scope for every call of an <see cref="OutboxDrainWorker{TProcessor}"/>.
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>
    /// How many due entries the worker asks for at a time.
    /// </summary>
    static abstract int BatchSize { get; }

    /// <summary>
    /// Queues whatever drifted from the outbox while the application was down; returns how many entries it queued.
    /// </summary>
    Task<int> ReconcileAsync(CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetDueAsync(int limit, CancellationToken ct);

    /// <summary>
    /// Processes one entry, recording its failure and next attempt itself; it throws only for unexpected faults.
    /// </summary>
    Task ProcessEntryAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// When the earliest waiting retry becomes due, or <see langword="null"/> when nothing waits.
    /// </summary>
    Task<DateTimeOffset?> GetNextDueAtAsync(CancellationToken ct);
}
