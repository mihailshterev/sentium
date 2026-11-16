namespace Sentium.Infrastructure.Retention;

/// <summary>
/// Deletes a module's records that are older than its retention period. Registered with
/// <see cref="RetentionExtensions.AddRetentionSweep{TSweep}"/> and run daily by the shared retention worker, in a
/// system scope so it reaches every user's rows.
/// </summary>
public interface IRetentionSweep
{
    /// <returns>The number of deleted records.</returns>
    Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct);
}
