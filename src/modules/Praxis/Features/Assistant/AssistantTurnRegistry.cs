using System.Collections.Concurrent;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// The stop switch of every turn from the moment it is queued, so a stop request reaches a turn even before it starts.
/// </summary>
internal sealed class AssistantTurnRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _turns = new(StringComparer.Ordinal);

    public void Track(string turnId, Guid ownerId)
    {
        _turns[turnId] = new Entry(ownerId);
    }

    /// <summary>
    /// Fires when the turn's owner stops it; <see cref="CancellationToken.None"/> for a turn that is not tracked.
    /// </summary>
    public CancellationToken StopToken(string turnId)
    {
        return _turns.TryGetValue(turnId, out var entry) ? entry.Stop.Token : CancellationToken.None;
    }

    /// <summary>
    /// Stops the turn when it is queued or running and belongs to <paramref name="ownerId"/>.
    /// </summary>
    public bool CancelOwned(string turnId, Guid ownerId)
    {
        if (!_turns.TryGetValue(turnId, out var entry) || entry.OwnerId != ownerId)
        {
            return false;
        }

        try
        {
            entry.Stop.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            // The turn ended between the lookup and the cancel.
            return false;
        }
    }

    public void Release(string turnId)
    {
        if (_turns.TryRemove(turnId, out var entry))
        {
            entry.Stop.Dispose();
        }
    }

    /// <summary>
    /// Its token source is disposed by <see cref="Release"/>, which every tracked turn reaches.
    /// </summary>
    private sealed class Entry(Guid ownerId)
    {
        public Guid OwnerId { get; } = ownerId;

        public CancellationTokenSource Stop { get; } = new();
    }
}
