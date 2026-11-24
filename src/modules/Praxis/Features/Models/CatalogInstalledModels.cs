using Microsoft.Extensions.Caching.Memory;

namespace Sentium.Praxis.Features.Models;

/// <summary>
/// Reads the declared models from the <see cref="IModelCatalog"/>, cached for a minute so building an agent rarely
/// costs a round-trip.
/// </summary>
internal sealed class CatalogInstalledModels(IModelCatalog catalog, IMemoryCache cache, ILogger<CatalogInstalledModels> logger) : IInstalledModels
{
    private const string CacheKey = "praxis:installed-models";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    public async ValueTask<string> PickAsync(IReadOnlyList<string> candidates, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfZero(candidates.Count);

        var requested = candidates[0];
        if (candidates.Count == 1 || await GetDeclaredAsync(ct) is not { } declared || declared.Contains(requested))
        {
            return requested;
        }

        var available = candidates.Skip(1).FirstOrDefault(declared.Contains);
        if (available is null)
        {
            return requested;
        }

        logger.LogWarning("Model {Requested} is not installed any more; using {Model} instead.", requested, available);
        return available;
    }

    private async ValueTask<HashSet<string>?> GetDeclaredAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out HashSet<string>? cached))
        {
            return cached;
        }

        // Not cached when unreachable, so the next build asks again.
        if (await catalog.ListAsync(ct) is not { } models)
        {
            return null;
        }

        var declared = models.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        cache.Set(CacheKey, declared, CacheTtl);
        return declared;
    }
}
