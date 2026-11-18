using Microsoft.Extensions.Options;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Memoria.Features.Indexing;

namespace Sentium.Memoria.Features.Notes;

/// <summary>
/// Embedding search over one user's vault chunks, collapsed to the best chunk per note.
/// </summary>
internal sealed class VaultSemanticSearch(
    IEmbeddingService embeddingService,
    IVectorRepository vectorRepository,
    IOptions<RagOptions> ragOptions)
{
    public const int MaxChunks = 100;

    public async Task<IReadOnlyList<SemanticNoteHit>> SearchAsync(Guid userId, string text, int chunkLimit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var embedding = await embeddingService.GenerateEmbeddingAsync(text, ct);
        var hits = await vectorRepository.SearchAsync(
            KnowledgeCollections.VaultNotes,
            embedding,
            Math.Clamp(chunkLimit, 1, MaxChunks),
            ragOptions.Value.ScoreThreshold,
            new KnowledgeScopeFilter(userId),
            ct);

        return [.. hits
            .Where(h => h.Chunk.UserId == userId)
            .Select(h => (Hit: h, NoteId: h.Chunk.Metadata.TryGetValue(NoteKnowledgeDocument.NoteIdKey, out var id) && Guid.TryParse(id, out var noteId) ? noteId : Guid.Empty))
            .Where(x => x.NoteId != Guid.Empty)
            .GroupBy(x => x.NoteId)
            .Select(g => g.MaxBy(x => x.Hit.Score))
            .OrderByDescending(x => x.Hit.Score)
            .Select(x => new SemanticNoteHit(x.NoteId, x.Hit.Score, x.Hit.Chunk.Content))];
    }
}
