using Sentium.Vigil.Entities;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Per-minute totals of the GenAI measurements <see cref="GenAiUsageListener"/> receives. Measurements arrive on
/// whichever thread made the request, so every member takes the lock.
/// </summary>
internal sealed class GenAiUsageAccumulator(TimeProvider timeProvider)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<UsageKey, InferenceUsageMinute> _buckets = [];

    public void RecordTokens(string caller, string model, string operation, bool isInput, long tokens)
    {
        lock (_gate)
        {
            var bucket = GetBucket(caller, model, operation);
            if (isInput)
            {
                bucket.InputTokens += tokens;
            }
            else
            {
                bucket.OutputTokens += tokens;
            }
        }
    }

    public void RecordDuration(string caller, string model, string operation, double seconds, bool failed)
    {
        lock (_gate)
        {
            var bucket = GetBucket(caller, model, operation);
            bucket.Requests++;
            bucket.Errors += failed ? 1 : 0;
            bucket.DurationSeconds += seconds;
        }
    }

    public void RecordFirstChunk(string caller, string model, string operation, double seconds)
    {
        lock (_gate)
        {
            var bucket = GetBucket(caller, model, operation);
            bucket.FirstChunkSeconds += seconds;
            bucket.FirstChunkCount++;
        }
    }

    /// <summary>
    /// Removes and returns every bucket of a minute before <paramref name="now"/>'s.
    /// </summary>
    public IReadOnlyList<InferenceUsageMinute> DrainCompleted(DateTimeOffset now)
    {
        var currentMinute = EpochMinutes.From(now);

        return Drain(key => key.Minute < currentMinute);
    }

    public IReadOnlyList<InferenceUsageMinute> DrainAll()
    {
        return Drain(_ => true);
    }

    private IReadOnlyList<InferenceUsageMinute> Drain(Func<UsageKey, bool> predicate)
    {
        lock (_gate)
        {
            var keys = _buckets.Keys.Where(predicate).ToList();
            var drained = new List<InferenceUsageMinute>(keys.Count);

            foreach (var key in keys)
            {
                drained.Add(_buckets[key]);
                _buckets.Remove(key);
            }

            return drained;
        }
    }

    private InferenceUsageMinute GetBucket(string caller, string model, string operation)
    {
        var key = new UsageKey(EpochMinutes.From(timeProvider.GetUtcNow()), caller, model, operation);

        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new InferenceUsageMinute { EpochMinute = key.Minute, Caller = caller, Model = model, Operation = operation };
            _buckets[key] = bucket;
        }

        return bucket;
    }

    private readonly record struct UsageKey(long Minute, string Caller, string Model, string Operation);
}
