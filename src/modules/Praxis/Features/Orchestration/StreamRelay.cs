using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Orchestration;

internal sealed class StreamRelay(IOptions<StreamingOptions> options, ILogger<StreamRelay> logger) : BackgroundService, IStreamRelay, IAgentStreamPublisher
{
    // Text tokens are coalesced into a few frames a second, so even a long answer stays far below these caps.
    private const int MaxBufferedEntries = 20_000;
    private const int MaxBufferedChars = 1_000_000;
    private const int MaxPendingChars = 8_192;
    private const int MaxSubscriberQueue = 8192;
    private const int MaxTrackedStreams = 1000;
    private static readonly TimeSpan CompletedGrace = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, StreamState> _streams = new(StringComparer.Ordinal);
    private readonly StreamingOptions _options = options.Value;

    public IAsyncEnumerable<StreamFrame> Subscribe(string streamId, long fromSeqExclusive, CancellationToken ct)
    {
        // Reading never creates a stream: only a claim or a publish does, so a reader cannot fill the relay.
        return _streams.TryGetValue(streamId, out var state)
            ? state.ReadAsync(fromSeqExclusive, ct)
            : AsyncEnumerable.Empty<StreamFrame>();
    }

    public void Claim(string streamId, Guid ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        GetOrCreate(streamId).OwnerId = ownerId;
    }

    public bool IsVisibleTo(string streamId, Guid userId)
    {
        // An unknown stream is visible: reading it yields only a "gone" frame, so nothing leaks.
        return !_streams.TryGetValue(streamId, out var state) || state.OwnerId is not { } owner || owner == userId;
    }

    public bool Exists(string streamId) => _streams.ContainsKey(streamId);

    public void Publish(string streamId, AgentStreamUpdate update)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);
        ArgumentNullException.ThrowIfNull(update);
        GetOrCreate(streamId).Publish(update);
    }

    public override void Dispose()
    {
        foreach (var state in _streams.Values)
        {
            state.Dispose();
        }

        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await SweepAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Stream relay shutting down.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Stream relay encountered a fatal error.");
        }
    }

    private StreamState GetOrCreate(string streamId) =>
        _streams.GetOrAdd(streamId, static (_, window) => new StreamState(window), _options.TextCoalesceWindow);

    private async Task SweepAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            var now = DateTime.UtcNow;

            foreach (var (id, state) in _streams)
            {
                if (state.IsEvictable(now, CompletedGrace, _options.StreamIdleTtl))
                {
                    Evict(id);
                }
            }

            var overflow = _streams.Count - MaxTrackedStreams;
            if (overflow > 0)
            {
                // A stream someone is watching is never evicted to make room.
                var victims = _streams
                    .Where(kvp => !kvp.Value.HasSubscribers)
                    .OrderBy(kvp => kvp.Value.LastActivityUtc)
                    .Take(overflow)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in victims)
                {
                    Evict(id);
                }
            }
        }
    }

    private void Evict(string streamId)
    {
        if (_streams.TryRemove(streamId, out var state))
        {
            state.Dispose();
        }
    }

    private sealed class StreamState(TimeSpan coalesceWindow) : IDisposable
    {
        private readonly Lock _gate = new();
        private readonly LinkedList<StreamFrame> _buffer = new();
        private readonly List<Channel<StreamFrame>> _subscribers = [];
        private long _seq;
        private int _bufferChars;
        private bool _completed;
        private DateTime _completedAtUtc;
        private Guid? _ownerId;

        private string? _pendingAuthor;
        private string? _pendingType;
        private StringBuilder? _pendingText;
        private Timer? _flushTimer;

        public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

        public Guid? OwnerId
        {
            get
            {
                lock (_gate)
                {
                    return _ownerId;
                }
            }

            set
            {
                lock (_gate)
                {
                    _ownerId = value;
                }
            }
        }

        public bool HasSubscribers
        {
            get
            {
                lock (_gate)
                {
                    return _subscribers.Count > 0;
                }
            }
        }

        /// <summary>
        /// Text frames (answer and reasoning tokens) are held for up to the coalesce window and joined with the tokens
        /// that follow, as long as author and type stay the same. Any other frame first releases the held text, so the
        /// order of updates never changes.
        /// </summary>
        public void Publish(AgentStreamUpdate update)
        {
            lock (_gate)
            {
                if (_completed)
                {
                    return;
                }

                LastActivityUtc = DateTime.UtcNow;

                if (coalesceWindow > TimeSpan.Zero && IsText(update.Type))
                {
                    HoldText(update);
                    return;
                }

                ReleaseHeldTextLocked();
                AppendLocked(update);
            }
        }

        public async IAsyncEnumerable<StreamFrame> ReadAsync(long fromSeqExclusive, [EnumeratorCancellation] CancellationToken ct)
        {
            List<StreamFrame> backlog;
            Channel<StreamFrame>? live = null;

            // The backlog is copied and the live queue registered under one lock, so every frame is delivered once.
            // The backlog is yielded directly: pushing it through the bounded live queue would drop frames of a long
            // stream.
            lock (_gate)
            {
                backlog = [.. _buffer.Where(frame => frame.Seq > fromSeqExclusive)];

                if (!_completed)
                {
                    live = Channel.CreateBounded<StreamFrame>(new BoundedChannelOptions(MaxSubscriberQueue)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        SingleReader = true,
                        SingleWriter = false
                    });
                    _subscribers.Add(live);
                }
            }

            try
            {
                foreach (var frame in backlog)
                {
                    yield return frame;
                }

                if (live is null)
                {
                    yield break;
                }

                await foreach (var frame in live.Reader.ReadAllAsync(ct))
                {
                    yield return frame;
                }
            }
            finally
            {
                if (live is not null)
                {
                    lock (_gate)
                    {
                        _subscribers.Remove(live);
                    }
                }
            }
        }

        public bool IsEvictable(DateTime now, TimeSpan completedGrace, TimeSpan idleTtl)
        {
            lock (_gate)
            {
                if (_subscribers.Count > 0)
                {
                    return false;
                }

                if (_completed && now - _completedAtUtc > completedGrace)
                {
                    return true;
                }

                return now - LastActivityUtc > idleTtl;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _flushTimer?.Dispose();
                _flushTimer = null;
            }
        }

        private static bool IsText(string type) => type is AgentUpdateTypes.Message or AgentUpdateTypes.Thought;

        private static bool IsTerminal(string type) => type is AgentUpdateTypes.Done or AgentUpdateTypes.Error or AgentUpdateTypes.Cancelled;

        private void HoldText(AgentStreamUpdate update)
        {
            if (_pendingText is not null && _pendingType == update.Type && _pendingAuthor == update.Author)
            {
                _pendingText.Append(update.Text);
                if (_pendingText.Length >= MaxPendingChars)
                {
                    ReleaseHeldTextLocked();
                }

                return;
            }

            ReleaseHeldTextLocked();

            _pendingAuthor = update.Author;
            _pendingType = update.Type;
            _pendingText = new StringBuilder(update.Text);

            _flushTimer ??= new Timer(static state => ((StreamState)state!).ReleaseHeldText(), this, Timeout.Infinite, Timeout.Infinite);
            _flushTimer.Change(coalesceWindow, Timeout.InfiniteTimeSpan);
        }

        private void ReleaseHeldText()
        {
            lock (_gate)
            {
                ReleaseHeldTextLocked();
            }
        }

        private void ReleaseHeldTextLocked()
        {
            if (_pendingText is null)
            {
                return;
            }

            var update = new AgentStreamUpdate(_pendingAuthor!, _pendingText.ToString(), _pendingType!);
            _pendingAuthor = null;
            _pendingType = null;
            _pendingText = null;

            AppendLocked(update);
        }

        private void AppendLocked(AgentStreamUpdate update)
        {
            var frame = new StreamFrame(++_seq, update);

            _buffer.AddLast(frame);
            _bufferChars += update.Text?.Length ?? 0;

            while (_buffer.Count > MaxBufferedEntries || _bufferChars > MaxBufferedChars)
            {
                _bufferChars -= _buffer.First!.Value.Update.Text?.Length ?? 0;
                _buffer.RemoveFirst();
            }

            // A subscriber too slow to keep up is disconnected rather than silently losing frames: its client reconnects
            // with its last id and catches up from the buffer.
            for (var i = _subscribers.Count - 1; i >= 0; i--)
            {
                if (!_subscribers[i].Writer.TryWrite(frame))
                {
                    _subscribers[i].Writer.TryComplete();
                    _subscribers.RemoveAt(i);
                }
            }

            if (IsTerminal(update.Type))
            {
                _completed = true;
                _completedAtUtc = DateTime.UtcNow;
                foreach (var sub in _subscribers)
                {
                    sub.Writer.TryComplete();
                }

                _subscribers.Clear();
                _flushTimer?.Dispose();
                _flushTimer = null;
            }
        }
    }
}
