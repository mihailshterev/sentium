using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Core.Rag.Models;
using Microsoft.Extensions.Logging;

namespace Sentium.AgentRuntime.Infrastructure.Learnings;

public sealed class AgentLearningService(
    IAgentLearningRepository repository,
    IDocumentIngestionService ingestionService,
    IVectorRepository vectorRepository,
    ILearningSanitizationPipeline sanitizationPipeline,
    ILogger<AgentLearningService> logger) : IAgentLearningService
{
    private const string LearningsCollection = "agent_learnings";
    private const string SourcePrefix = "learning:";

    public Task<IReadOnlyList<AgentLearningResponse>> GetLearningsAsync(
        string? agentName = null,
        int count = 50,
        CancellationToken ct = default)
        => repository.GetAllAsync(agentName, count, ct);

    public Task<AgentLearningStats> GetStatsAsync(CancellationToken ct = default)
        => repository.GetStatsAsync(ct);

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

        await IngestLearningAsync(entity, ct);

        return new AgentLearningResponse(
            entity.Id, entity.AgentName, entity.Content, entity.Tags,
            entity.ConversationId, entity.CapturedAt, entity.IsIngested, entity.IsGlobal);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await repository.FindAsync(id, ct);
        if (entity is null)
        {
            return;
        }

        await vectorRepository.DeleteBySourceAsync(LearningsCollection, $"{SourcePrefix}{id}", ct);

        await repository.RemoveAsync(entity, ct);
    }

    public async Task<AgentLearningResponse> UpdateAsync(Guid id, UpdateAgentLearningRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await repository.FindAsync(id, ct) ?? throw new KeyNotFoundException($"AgentLearning {id} not found.");

        await vectorRepository.DeleteBySourceAsync(LearningsCollection, $"{SourcePrefix}{id}", ct);

        entity.Content = request.Content;
        entity.Tags = request.Tags;
        entity.IsIngested = false;

        await repository.SaveAsync(ct);

        await IngestLearningAsync(entity, ct);

        return new AgentLearningResponse(
            entity.Id, entity.AgentName, entity.Content, entity.Tags,
            entity.ConversationId, entity.CapturedAt, entity.IsIngested, entity.IsGlobal);
    }

    private async Task IngestLearningAsync(AgentLearning entity, CancellationToken ct)
    {
        if (!entity.IsGlobal && entity.UserId is null)
        {
            logger.LogInformation("Skipping vector ingestion for ownerless, non-global learning {LearningId} — no retrieval scope.", entity.Id);
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
            # Agent Learning — {entity.AgentName}

            **Captured:** {entity.CapturedAt:yyyy-MM-dd HH:mm UTC}{tagsSection}

            {entity.Content}
            """;
    }
}
