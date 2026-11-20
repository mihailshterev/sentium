namespace Sentium.Praxis.Contracts;

/// <summary>
/// Told once when a Schedule's run ends, whatever the outcome. Implementations must not throw.
/// </summary>
public interface IUnattendedRunObserver
{
    Task OnRunCompletedAsync(UnattendedRunCompletion completion, CancellationToken ct);
}
