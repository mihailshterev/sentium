using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Sentium.Infrastructure.Caching;

namespace Sentium.AgentRuntime.Infrastructure.Learnings;

public sealed class AgentLearningService(
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

    public async Task<IReadOnlyList<AgentLearningResponse>> GetLearningsAsync(
        string? agentName = null,
        int count = 50,
        CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:{agentName ?? "all"}:{count}",
            async token => await repository.GetAllAsync(agentName, count, token),
            CacheTag,
            ct);

    public async Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"{CacheTag}:stats",
            async token => await repository.GetStatsAsync(token),
            CacheTag,
            ct);

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

        await repository.AddAsync(entity, ct);
        await cache.InvalidateTagAsync(CacheTag, ct);

        await IngestLearningAsync(entity, ct);

        return new AgentLearningResponse(
            entity.Id, entity.AgentName, entity.Content, entity.Tags,
            entity.ConversationId, entity.CapturedAt, entity.IsIngested, entity.IsGlobal);
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

        return new AgentLearningResponse(
            entity.Id, entity.AgentName, entity.Content, entity.Tags,
            entity.ConversationId, entity.CapturedAt, entity.IsIngested, entity.IsGlobal);
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
                    { "learning_id", entity.Id.ToString() },
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
}
