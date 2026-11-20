namespace Sentium.Infrastructure.BackgroundWork;

/// <summary>
/// Reports the work a module is doing in the background, for the background-jobs overview. Register implementations as
/// <see cref="IBackgroundActivitySource"/> services. Reading must be cheap and must not throw.
/// </summary>
public interface IBackgroundActivitySource
{
    Task<IReadOnlyList<BackgroundActivity>> GetActivitiesAsync(CancellationToken ct);
}
