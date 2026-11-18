using Sentium.Core.Results;
using Sentium.Memoria.Contracts.Models;

namespace Sentium.Memoria.Features.Notes;

/// <summary>
/// The signed-in user's own vault, for the portal. Every method returns nothing (or <see langword="false"/>) when the
/// user has not synced a vault.
/// </summary>
internal interface IVaultQueryService
{
    Task<VaultSummaryDto?> GetSummaryAsync(Guid userId, CancellationToken ct = default);

    Task<PagedResponse<VaultNoteSummaryDto>> ListNotesAsync(Guid userId, VaultNotesQuery query, CancellationToken ct = default);

    Task<VaultNoteDto?> GetNoteAsync(Guid userId, Guid noteId, CancellationToken ct = default);

    Task<IReadOnlyList<VaultTagCount>> GetTagsAsync(Guid userId, CancellationToken ct = default);

    Task<VaultGraphDto> GetGraphAsync(Guid userId, string? tag, int limit, CancellationToken ct = default);

    /// <summary>
    /// Deletes the vault and every synced note; their knowledge-base chunks are removed in the background. The next
    /// sync from Satelles starts over with a new claim.
    /// </summary>
    Task<bool> ForgetAsync(Guid userId, CancellationToken ct = default);
}
