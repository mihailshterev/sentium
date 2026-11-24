namespace Sentium.Praxis.Features.Retention;

/// <summary>
/// The runs the previous <see cref="OrphanedArtifactSweep"/> found orphaned, kept between sweeps (in memory, so a
/// restart starts over).
/// </summary>
internal sealed class OrphanedArtifactSuspects
{
    private readonly Lock _gate = new();
    private HashSet<Guid> _previous = [];

    /// <summary>
    /// Remembers <paramref name="orphaned"/> for the next sweep and returns those that were already orphaned last time.
    /// </summary>
    public IReadOnlyList<Guid> Confirm(IReadOnlySet<Guid> orphaned)
    {
        ArgumentNullException.ThrowIfNull(orphaned);

        lock (_gate)
        {
            var confirmed = orphaned.Where(_previous.Contains).ToList();
            _previous = [.. orphaned.Except(confirmed)];
            return confirmed;
        }
    }
}
