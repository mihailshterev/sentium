using Sentium.Praxis.Entities;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Infrastructure.Caching;
using Sentium.Core.Results;

namespace Sentium.Praxis.Features.Learnings;

internal sealed class AgentLearningService(
    IAgentLearningRepository repository,
    IDocumentIngestionService ingestionService,
    IVectorRepository vectorRepository,
    IEmbeddingService embeddingService,
    ILearningSanitizationPipeline sanitizationPipeline,
    IScopedCache cache,
    ILogger<AgentLearningService> logger) : IAgentLearningService
{
    private const string LearningsCollection = KnowledgeCollections.AgentLearnings;
    private const string SourcePrefix = "learning:";
    private const string CacheTag = "learnings";
    private const float RecallScoreThreshold = 0.35f;
    private const string LearningIdKey = "learning_id";

    /// <summary>
    /// Cosine similarity at which a new private learning says what an existing one (private or global) already says.
    /// </summary>
    private const float DuplicateScoreThreshold = 0.92f;

    public async Task<PagedResponse<AgentLearningResponse>> GetLearningsAsync(
        string? agentName,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();

        return await cache.GetOrCreateAsync(
            $"{CacheTag}:{agentName ?? "all"}:page:{page}:{pageSize}",
            token => LoadPageAsync(agentName, page, pageSize, token),
            CacheTag,
            ct);
    }

    public async Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"{CacheTag}:stats",
            async token => await repository.GetStatsAsync(token),
            CacheTag,
            ct);
    }

    public async Task<AgentLearningResponse> CaptureAsync(CaptureAgentLearningRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = request.Content;
        var isGlobal = false;

        if (request.RequestGlobal)
        {
            var verdict = await sanitizationPipeline.EvaluateForGlobalAsync(request.Content, request.Tags, request.AgentName, ct);

            if (verdict.DuplicateOfId is { } duplicateId)
            {
                logger.LogInformation("Skipped global capture for agent {AgentName}: equivalent global learning {LearningId} already exists.", request.AgentName, duplicateId);

                return new AgentLearningResponse(duplicateId, request.AgentName, verdict.SanitizedContent, request.Tags, request.ConversationId, DateTimeOffset.UtcNow, IsIngested: true, IsGlobal: true);
            }

            isGlobal = verdict.Approved;
            content = verdict.Approved ? verdict.SanitizedContent : request.Content;

            if (!verdict.Approved)
            {
                logger.LogInformation("Global capture for agent {AgentName} kept private: {Reason}", request.AgentName, verdict.Reason);
            }
        }

        var entity = new AgentLearning
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            IsGlobal = isGlobal,
            AgentName = request.AgentName,
            Content = content,
            Tags = request.Tags,
            ConversationId = request.ConversationId,
            CapturedAt = DateTimeOffset.UtcNow,
            IsIngested = false
        };

        // Compared in the form learnings are embedded in, so the header does not hide an identical body.
        if (!isGlobal && await FindNearDuplicateAsync(FormatAsMarkdown(entity), request.UserId, ct) is { } duplicate)
        {
            logger.LogInformation("Skipped capture for agent {AgentName}: learning {LearningId} already says the same.", request.AgentName, duplicate.Id);
            return ToResponse(duplicate);
        }

        await repository.AddAsync(entity, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);

        await IngestLearningAsync(entity, ct);

        return ToResponse(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.FindAsync(id, ct);
        if (entity is null)
        {
            return false;
        }

        await vectorRepository.DeleteBySourceAsync(LearningsCollection, $"{SourcePrefix}{id}", ct);
        await repository.RemoveAsync(entity, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);
        return true;
    }

    public async Task<AgentLearningResponse?> UpdateAsync(Guid id, UpdateAgentLearningRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await repository.FindAsync(id, ct);
        if (entity is null)
        {
            return null;
        }

        await vectorRepository.DeleteBySourceAsync(LearningsCollection, $"{SourcePrefix}{id}", ct);

        entity.Content = request.Content;
        entity.Tags = request.Tags;
        entity.IsIngested = false;

        await repository.SaveAsync(ct);
        await cache.InvalidateTagAsync(CacheTag, ct);

        await IngestLearningAsync(entity, ct);

        return ToResponse(entity);
    }

    public async Task<IReadOnlyList<RecalledLearning>> RecallRelevantAsync(string query, Guid? userId, int limit = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(query, ct);

            var results = await vectorRepository.SearchAsync(
                LearningsCollection,
                embedding,
                topK: Math.Clamp(limit, 1, 20),
                scoreThreshold: RecallScoreThreshold,
                scope: new KnowledgeScopeFilter(userId),
                ct: ct);

            if (results.Count == 0)
            {
                return [];
            }

            await RecordRecallAsync(results, ct);

            return results
                .Select(r => new RecalledLearning(
                    r.Chunk.Content,
                    r.Score,
                    r.Chunk.Metadata.TryGetValue("agent_name", out var agentName) ? agentName : string.Empty))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to recall relevant learnings for query");
            return [];
        }
    }

    public async Task<int> RetryPendingIngestionAsync(int max, CancellationToken ct = default)
    {
        var ingested = 0;
        foreach (var learning in await repository.GetPendingIngestionAsync(max, ct))
        {
            await IngestLearningAsync(learning, ct);
            if (!learning.IsIngested)
            {
                // The embedding server is most likely still away; the rest waits for the next attempt.
                break;
            }

            ingested++;
        }

        if (ingested > 0)
        {
            await cache.InvalidateTagAsync(CacheTag, ct);
        }

        return ingested;
    }

    private static AgentLearningResponse ToResponse(AgentLearning entity)
    {
        return new AgentLearningResponse(
            entity.Id, entity.AgentName, entity.Content, entity.Tags, entity.ConversationId, entity.CapturedAt,
            entity.IsIngested, entity.IsGlobal, entity.RecallCount, entity.LastRecalledAt);
    }

    /// <summary>
    /// An existing learning the user's agents can see (their own or a global one) that says nearly the same thing.
    /// Best effort: without the embedding server there is no comparison, and the learning is simply captured.
    /// </summary>
    private async Task<AgentLearning?> FindNearDuplicateAsync(string embeddedText, Guid? userId, CancellationToken ct)
    {
        if (userId is null)
        {
            return null;
        }

        try
        {
            var embedding = await embeddingService.GenerateEmbeddingAsync(embeddedText, ct);
            var hits = await vectorRepository.SearchAsync(
                LearningsCollection, embedding, topK: 1, scoreThreshold: DuplicateScoreThreshold, scope: new KnowledgeScopeFilter(userId), ct: ct);

            return hits.Count > 0 && LearningIdOf(hits[0]) is { } id ? await repository.FindAsync(id, ct) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not check a new learning for duplicates; capturing it anyway.");
            return null;
        }
    }

    private async Task RecordRecallAsync(IReadOnlyList<VectorSearchResult> results, CancellationToken ct)
    {
        var ids = results.Select(LearningIdOf).OfType<Guid>().Distinct().ToList();

        try
        {
            await repository.RecordRecallAsync(ids, DateTimeOffset.UtcNow, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not record the recall of {Count} learnings.", ids.Count);
        }
    }

    private static Guid? LearningIdOf(VectorSearchResult result) =>
        result.Chunk.Metadata.TryGetValue(LearningIdKey, out var value) && Guid.TryParse(value, out var id) ? id : null;

    private async Task IngestLearningAsync(AgentLearning entity, CancellationToken ct)
    {
        if (!entity.IsGlobal && entity.UserId is null)
        {
            logger.LogInformation("Skipping vector ingestion for ownerless, non-global learning {LearningId} - no retrieval scope.", entity.Id);
            return;
        }

        try
        {
            var markdownContent = FormatAsMarkdown(entity);

            var request = new IngestionRequest
            {
                Content = markdownContent,
                Source = $"{SourcePrefix}{entity.Id}",
                SourceType = IngestionSourceType.AgentLearning,
                Scope = entity.IsGlobal ? KnowledgeScope.Shared : KnowledgeScope.User,
                UserId = entity.IsGlobal ? null : entity.UserId,
                Metadata = new Dictionary<string, string>
                {
                    { "agent_name", entity.AgentName },
                    { "tags", entity.Tags },
                    { "captured_at", entity.CapturedAt.ToString("O") },
                    { LearningIdKey, entity.Id.ToString() },
                    { "origin_user_id", entity.UserId?.ToString() ?? string.Empty }
                }
            };

            await ingestionService.IngestAsync(request, LearningsCollection, ct);

            entity.IsIngested = true;
            await repository.SaveAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest learning {LearningId} for agent {AgentName}", entity.Id, entity.AgentName);
        }
    }

    private static string FormatAsMarkdown(AgentLearning entity)
    {
        var tagsSection = string.IsNullOrWhiteSpace(entity.Tags)
            ? string.Empty
            : $"\n**Tags:** {entity.Tags}";

        return $"""
            # Agent Learning - {entity.AgentName}

            **Captured:** {entity.CapturedAt:yyyy-MM-dd HH:mm UTC}{tagsSection}

            {entity.Content}
            """;
    }

    private async ValueTask<PagedResponse<AgentLearningResponse>> LoadPageAsync(string? agentName, int page, int pageSize, CancellationToken ct)
    {
        var (items, total) = await repository.GetAllAsync(agentName, page, pageSize, ct);
        return PagedResponse<AgentLearningResponse>.Create(items, total, page, pageSize);
    }
}
