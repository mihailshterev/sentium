using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Entities;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Turns successive llama-server snapshots into per-minute deltas. Not thread-safe: the collector owns it.
/// </summary>
internal sealed class ServerMetricsAccumulator
{
    private static readonly LlamaServerSnapshot Zero = new(0, 0, 0, 0, 0, 0);

    private readonly Dictionary<(InferenceEndpoint Server, string Model), LlamaServerSnapshot> _previous = [];
    private readonly Dictionary<(long Minute, InferenceEndpoint Server, string Model), InferenceServerMinute> _buckets = [];

    public void Observe(InferenceEndpoint server, string model, LlamaServerSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(snapshot);

        var bucket = GetBucket(EpochMinutes.From(now), server, model);
        bucket.Samples++;
        bucket.BusySamples += snapshot.RequestsProcessing > 0 ? 1 : 0;
        bucket.PeakRequestsProcessing = Math.Max(bucket.PeakRequestsProcessing, snapshot.RequestsProcessing);
        bucket.PeakRequestsDeferred = Math.Max(bucket.PeakRequestsDeferred, snapshot.RequestsDeferred);

        // The first snapshot after Vigil starts only sets the baseline: what the server counted before is unknown time.
        if (_previous.TryGetValue((server, model), out var previous))
        {
            // Counters only fall when the model's server process restarted, so all it counted since is new work.
            var restarted = snapshot.PromptTokensTotal < previous.PromptTokensTotal || snapshot.GeneratedTokensTotal < previous.GeneratedTokensTotal;
            var baseline = restarted ? Zero : previous;

            bucket.PromptTokens += (long)Delta(snapshot.PromptTokensTotal, baseline.PromptTokensTotal);
            bucket.PromptSeconds += Delta(snapshot.PromptSecondsTotal, baseline.PromptSecondsTotal);
            bucket.GeneratedTokens += (long)Delta(snapshot.GeneratedTokensTotal, baseline.GeneratedTokensTotal);
            bucket.GenerationSeconds += Delta(snapshot.GenerationSecondsTotal, baseline.GenerationSecondsTotal);
        }

        _previous[(server, model)] = snapshot;
    }

    /// <summary>
    /// Records that the model is not loaded, so its next snapshot counts from zero (a fresh server process) instead
    /// of only setting a baseline.
    /// </summary>
    public void MarkNotLoaded(InferenceEndpoint server, string model)
    {
        _previous[(server, model)] = Zero;
    }

    /// <summary>
    /// Removes and returns every bucket of a minute before <paramref name="now"/>'s.
    /// </summary>
    public IReadOnlyList<InferenceServerMinute> DrainCompleted(DateTimeOffset now)
    {
        var currentMinute = EpochMinutes.From(now);

        return Drain(key => key.Minute < currentMinute);
    }

    public IReadOnlyList<InferenceServerMinute> DrainAll()
    {
        return Drain(_ => true);
    }

    private IReadOnlyList<InferenceServerMinute> Drain(Func<(long Minute, InferenceEndpoint Server, string Model), bool> predicate)
    {
        var keys = _buckets.Keys.Where(predicate).ToList();
        var drained = new List<InferenceServerMinute>(keys.Count);

        foreach (var key in keys)
        {
            drained.Add(_buckets[key]);
            _buckets.Remove(key);
        }

        return drained;
    }

    private InferenceServerMinute GetBucket(long minute, InferenceEndpoint server, string model)
    {
        if (!_buckets.TryGetValue((minute, server, model), out var bucket))
        {
            bucket = new InferenceServerMinute { EpochMinute = minute, Server = server, Model = model };
            _buckets[(minute, server, model)] = bucket;
        }

        return bucket;
    }

    private static double Delta(double current, double baseline)
    {
        return Math.Max(0, current - baseline);
    }
}
