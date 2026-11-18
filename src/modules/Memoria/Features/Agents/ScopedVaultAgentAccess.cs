using Sentium.Infrastructure.Security;
using Sentium.Memoria.Contracts;
using Sentium.Memoria.Contracts.Models;
using Sentium.Memoria.Features.Notes;

namespace Sentium.Memoria.Features.Agents;

/// <summary>
/// Singleton facade over <see cref="VaultReader"/> for other modules. Each call runs in its own DI scope marked as a
/// system scope BEFORE anything is resolved: agent runs have no HTTP user, so the DbContext's per-user filter would
/// otherwise hide everything. <see cref="VaultReader"/> filters on the explicit user id of every call instead.
/// </summary>
internal sealed class ScopedVaultAgentAccess(IServiceScopeFactory scopeFactory) : IVaultAgentAccess
{
    private const int MaxTopK = 20;

    public Task<VaultOverview?> GetOverviewAsync(Guid userId, CancellationToken ct = default)
        => RunAsync(r => r.GetOverviewAsync(userId, ct));

    public Task<IReadOnlyList<VaultSearchHit>> SearchAsync(Guid userId, VaultSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clamped = query with { TopK = Math.Clamp(query.TopK, 1, MaxTopK) };
        return RunAsync(r => r.SearchAsync(userId, clamped, ct));
    }

    public Task<VaultNoteDetail?> GetNoteAsync(Guid userId, string note, int maxChars, CancellationToken ct = default)
        => RunAsync(r => r.GetNoteDetailAsync(userId, note, Math.Max(maxChars, 1), ct));

    public Task<IReadOnlyList<VaultRelatedNote>?> GetRelatedAsync(Guid userId, string note, int topK, CancellationToken ct = default)
        => RunAsync(r => r.GetRelatedAsync(userId, note, Math.Clamp(topK, 1, MaxTopK), ct));

    public Task<IReadOnlyList<VaultRelatedNote>?> SuggestLinksAsync(Guid userId, string note, int topK, CancellationToken ct = default)
        => RunAsync(r => r.SuggestLinksAsync(userId, note, Math.Clamp(topK, 1, MaxTopK), ct));

    private async Task<T> RunAsync<T>(Func<VaultReader, Task<T>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
        return await action(scope.ServiceProvider.GetRequiredService<VaultReader>());
    }
}
