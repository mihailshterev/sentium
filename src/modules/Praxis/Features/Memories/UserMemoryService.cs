using Microsoft.EntityFrameworkCore;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Memories;

/// <remarks>
/// The vector goes in first and the row after (removing the vector again if the row fails), and recall only returns
/// vectors that have a row, so a half-finished write is never recalled.
/// </remarks>
internal sealed class UserMemoryService(
    PraxisDbContext db,
    IEmbeddingService embeddings,
    IVectorRepository vectors,
    TimeProvider timeProvider) : IUserMemoryService
{
    public const int MaxContentLength = 2_000;
    public const string MemoryIdMetadataKey = "memory_id";

    private const string SourcePrefix = "memory:";
    private const string Collection = KnowledgeCollections.UserMemories;

    /// <summary>
    /// Cosine similarity at which a new memory restates an existing one (same fact, different words) and replaces it.
    /// </summary>
    private const float DuplicateScoreThreshold = 0.92f;

    private const float RecallScoreThreshold = 0.7f;
    private const int MaxRecallLimit = 20;

    public static string SourceOf(Guid memoryId) => $"{SourcePrefix}{memoryId}";

    public static IReadOnlyDictionary<string, string> MetadataOf(Guid memoryId) =>
        new Dictionary<string, string> { [MemoryIdMetadataKey] = memoryId.ToString() };

    public async Task<IReadOnlyList<UserMemoryDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        return await Owned(userId)
            .AsNoTracking()
            .OrderByDescending(m => m.UpdatedAt)
            .Select(m => new UserMemoryDto(m.Id, m.Content, m.CreatedAt, m.UpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<RememberResult> RememberAsync(Guid userId, string content, CancellationToken ct = default)
    {
        content = Normalize(content);
        var embedding = await embeddings.GenerateEmbeddingAsync(content, ct);

        if (await FindNearDuplicateAsync(userId, embedding, ct) is { } existing)
        {
            return new RememberResult(await ReplaceAsync(existing, content, embedding, ct), ReplacedExisting: true);
        }

        var now = timeProvider.GetUtcNow();
        var memory = new UserMemory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = content,
            CreatedAt = now,
            UpdatedAt = now
        };

        await vectors.UpsertAsync(Collection, ToChunk(memory), embedding, ct);

        try
        {
            db.UserMemories.Add(memory);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await vectors.DeleteBySourceAsync(Collection, SourceOf(memory.Id), CancellationToken.None);
            throw;
        }

        return new RememberResult(ToDto(memory), ReplacedExisting: false);
    }

    public async Task<UserMemoryDto?> UpdateAsync(Guid userId, Guid memoryId, string content, CancellationToken ct = default)
    {
        var memory = await Owned(userId).FirstOrDefaultAsync(m => m.Id == memoryId, ct);
        if (memory is null)
        {
            return null;
        }

        content = Normalize(content);
        var embedding = await embeddings.GenerateEmbeddingAsync(content, ct);
        return await ReplaceAsync(memory, content, embedding, ct);
    }

    public async Task<bool> ForgetAsync(Guid userId, Guid memoryId, CancellationToken ct = default)
    {
        var deleted = await Owned(userId).Where(m => m.Id == memoryId).ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            return false;
        }

        await vectors.DeleteBySourceAsync(Collection, SourceOf(memoryId), ct);
        return true;
    }

    public async Task<IReadOnlyList<RecalledMemory>> RecallAsync(Guid userId, string query, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var embedding = await embeddings.GenerateEmbeddingAsync(query, ct);
        var hits = await vectors.SearchAsync(
            Collection,
            embedding,
            topK: Math.Clamp(limit, 1, MaxRecallLimit),
            scoreThreshold: RecallScoreThreshold,
            scope: new KnowledgeScopeFilter(userId),
            ct: ct);

        var ids = hits.Select(h => h.Chunk.Id).ToList();
        var rows = await Owned(userId)
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        return [.. hits
            .Where(h => rows.ContainsKey(h.Chunk.Id))
            .Select(h => new RecalledMemory(ToDto(rows[h.Chunk.Id]), h.Score))];
    }

    /// <summary>
    /// Explicitly the user's own rows: the query filter would let a Sovereign see everyone's.
    /// </summary>
    private IQueryable<UserMemory> Owned(Guid userId)
    {
        return db.UserMemories.IgnoreQueryFilters().Where(m => m.UserId == userId);
    }

    private async Task<UserMemory?> FindNearDuplicateAsync(Guid userId, float[] embedding, CancellationToken ct)
    {
        var hits = await vectors.SearchAsync(
            Collection,
            embedding,
            topK: 1,
            scoreThreshold: DuplicateScoreThreshold,
            scope: new KnowledgeScopeFilter(userId),
            ct: ct);

        if (hits.Count == 0)
        {
            return null;
        }

        var id = hits[0].Chunk.Id;
        return await Owned(userId).FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    private async Task<UserMemoryDto> ReplaceAsync(UserMemory memory, string content, float[] embedding, CancellationToken ct)
    {
        memory.Content = content;
        memory.UpdatedAt = timeProvider.GetUtcNow();

        await vectors.UpsertAsync(Collection, ToChunk(memory), embedding, ct);
        await db.SaveChangesAsync(ct);

        return ToDto(memory);
    }

    private static string Normalize(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var trimmed = content.Trim();
        return trimmed.Length <= MaxContentLength ? trimmed : trimmed[..MaxContentLength];
    }

    private static DocumentChunk ToChunk(UserMemory memory)
    {
        return new DocumentChunk
        {
            Id = memory.Id,
            Content = memory.Content,
            Source = SourceOf(memory.Id),
            SourceType = IngestionSourceType.UserInput,
            Metadata = MetadataOf(memory.Id),
            CreatedAt = memory.UpdatedAt,
            Scope = KnowledgeScope.User,
            UserId = memory.UserId
        };
    }

    private static UserMemoryDto ToDto(UserMemory memory) => new(memory.Id, memory.Content, memory.CreatedAt, memory.UpdatedAt);
}
