using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Speech;

namespace Sentium.Praxis.Features.Speech;

/// <summary>
/// Per-user request budgets for the speech endpoints, so one browser tab cannot keep the speech containers busy
/// for everyone. Counted here, not in the HTTP rate-limiting middleware: that runs before authentication and
/// does not know the user.
/// </summary>
internal sealed class SpeechRateLimiter : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly PartitionedRateLimiter<Guid> _transcriptions;
    private readonly PartitionedRateLimiter<Guid> _syntheses;

    public SpeechRateLimiter(IOptions<SpeechOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _transcriptions = CreateLimiter(options.Value.TranscriptionsPerMinute);
        _syntheses = CreateLimiter(options.Value.SynthesesPerMinute);
    }

    public bool TryAcquireTranscription(Guid userId) => TryAcquire(_transcriptions, userId);

    public bool TryAcquireSynthesis(Guid userId) => TryAcquire(_syntheses, userId);

    public void Dispose()
    {
        _transcriptions.Dispose();
        _syntheses.Dispose();
    }

    private static bool TryAcquire(PartitionedRateLimiter<Guid> limiter, Guid userId)
    {
        using var lease = limiter.AttemptAcquire(userId);
        return lease.IsAcquired;
    }

    private static PartitionedRateLimiter<Guid> CreateLimiter(int permitsPerMinute)
    {
        return PartitionedRateLimiter.Create<Guid, Guid>(userId => RateLimitPartition.GetFixedWindowLimiter(
            userId,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = Window,
                QueueLimit = 0
            }));
    }
}
