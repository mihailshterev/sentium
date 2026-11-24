using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentium.Praxis.Common;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Conversations;

internal sealed class ConversationRepository(PraxisDbContext context) : IConversationRepository
{
    private static readonly JsonSerializerOptions StepJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<(IReadOnlyList<ConversationSummary> Items, int TotalCount)> GetConversationsAsync(
        int page,
        int pageSize,
        string? search = null,
        bool archived = false,
        CancellationToken ct = default)
    {
        var query = context.Conversations.AsNoTracking().Where(c => c.IsArchived == archived);
        if (TextSearch.Normalize(search) is { } term)
        {
            var pattern = TextSearch.ToContainsPattern(term);
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, pattern, TextSearch.EscapeCharacter)
                || c.Messages.Any(m => EF.Functions.ILike(m.Content, pattern, TextSearch.EscapeCharacter)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastActivityAt ?? c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationSummary(
                c.Id,
                c.Title,
                c.Model,
                c.CreatedAt,
                c.AgentName,
                c.WorkspaceId,
                c.Workspace != null ? c.Workspace.Name : null,
                c.LastActivityAt ?? c.CreatedAt,
                c.IsPinned,
                c.IsArchived))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ConversationResponse?> GetConversationAsync(Guid conversationId, int messageLimit, CancellationToken ct = default)
    {
        var conv = await context.Conversations
            .AsNoTracking()
            .Include(c => c.Workspace)
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conv is null)
        {
            return null;
        }

        var page = await LoadMessagePageAsync(context.Messages.Where(m => m.ConversationId == conversationId), messageLimit, ct);

        return new ConversationResponse(
            conv.Id,
            conv.Title,
            conv.Model,
            conv.CreatedAt,
            page.Messages,
            conv.AgentName,
            conv.WorkspaceId,
            conv.Workspace?.Name,
            page.HasEarlierMessages,
            conv.IsPinned,
            conv.IsArchived);
    }

    public async Task<MessagePageResponse?> GetMessagesBeforeAsync(Guid conversationId, Guid beforeMessageId, int limit, CancellationToken ct = default)
    {
        var before = await context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Id == beforeMessageId)
            .Select(m => (DateTime?)m.Timestamp)
            .FirstOrDefaultAsync(ct);

        if (before is not { } timestamp)
        {
            return null;
        }

        return await LoadMessagePageAsync(
            context.Messages.Where(m => m.ConversationId == conversationId && m.Timestamp < timestamp),
            limit,
            ct);
    }

    public async Task<bool> SetPinnedAsync(Guid conversationId, bool pinned, CancellationToken ct = default)
    {
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPinned, pinned), ct);

        return affected > 0;
    }

    public async Task<bool> SetArchivedAsync(Guid conversationId, bool archived, CancellationToken ct = default)
    {
        // An archived conversation is out of the way, so it is not pinned either.
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsArchived, archived)
                .SetProperty(c => c.IsPinned, c => !archived && c.IsPinned), ct);

        return affected > 0;
    }

    public async Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = request.Title?.Trim();
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrEmpty(title) ? ConversationTitles.Placeholder : title,
            IsTitlePending = string.IsNullOrEmpty(title),
            Model = request.Model,
            AgentName = string.IsNullOrWhiteSpace(request.AgentName) ? null : request.AgentName.Trim(),
            WorkspaceId = request.WorkspaceId,
            CreatedAt = DateTime.UtcNow
        };

        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(ct);

        var workspaceName = request.WorkspaceId is { } workspaceId
            ? await context.Workspaces.Where(w => w.Id == workspaceId).Select(w => w.Name).FirstOrDefaultAsync(ct)
            : null;

        return new ConversationSummary(
            conversation.Id,
            conversation.Title,
            conversation.Model,
            conversation.CreatedAt,
            conversation.AgentName,
            conversation.WorkspaceId,
            workspaceName,
            conversation.CreatedAt);
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteDeleteAsync(ct);

        return affected > 0;
    }

    public async Task<string?> GetAgentNameAsync(Guid conversationId, Guid? userId, CancellationToken ct = default)
    {
        return userId is null
            ? null
            : await context.Conversations
                .AsNoTracking()
                .Where(c => c.Id == conversationId && c.UserId == userId)
                .Select(c => c.AgentName)
                .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsTitlePendingAsync(Guid conversationId, CancellationToken ct = default)
        => await context.Conversations.AnyAsync(c => c.Id == conversationId && c.IsTitlePending, ct);

    public async Task<bool> TrySetGeneratedTitleAsync(Guid conversationId, string title, CancellationToken ct = default)
    {
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId && c.IsTitlePending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Title, title)
                .SetProperty(c => c.IsTitlePending, false), ct);

        return affected > 0;
    }

    public async Task<bool> RenameConversationAsync(Guid conversationId, string title, CancellationToken ct = default)
    {
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Title, title)
                .SetProperty(c => c.IsTitlePending, false), ct);

        return affected > 0;
    }

    public async Task<bool> SetWorkspaceAsync(Guid conversationId, Guid? workspaceId, CancellationToken ct = default)
    {
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.WorkspaceId, workspaceId), ct);

        return affected > 0;
    }

    public async Task<Guid?> GetWorkspaceIdAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        return await context.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId && c.UserId == userId)
            .Select(c => c.WorkspaceId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> WorkspaceIsOwnedByAsync(Guid workspaceId, Guid userId, CancellationToken ct = default)
        => await context.Workspaces.AnyAsync(w => w.Id == workspaceId && w.UserId == userId, ct);

    public async Task AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        string? enhancedPrompt = null,
        IReadOnlyList<MessageStep>? steps = null,
        IReadOnlyList<MessageAttachment>? attachments = null,
        CancellationToken ct = default)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            EnhancedPrompt = enhancedPrompt,
            Steps = steps is { Count: > 0 } ? JsonSerializer.Serialize(steps, StepJson) : null,
            Attachments = attachments is { Count: > 0 } ? JsonSerializer.Serialize(attachments, StepJson) : null,
            Timestamp = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(ct);

        // New activity brings an archived conversation back into the list.
        await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.LastActivityAt, message.Timestamp)
                .SetProperty(c => c.IsArchived, false), ct);
    }

    public async Task<bool> IsOwnedByAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
        => await context.Conversations.AnyAsync(c => c.Id == conversationId && c.UserId == userId, ct);

    public async Task<IReadOnlyList<MessageResponse>> GetRecentMessagesAsync(Guid conversationId, Guid userId, int limit, CancellationToken ct = default)
    {
        var recent = await context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Conversation.UserId == userId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .Select(m => new { m.Id, m.Role, m.Content, m.Timestamp, m.EnhancedPrompt, m.Attachments })
            .ToListAsync(ct);

        recent.Reverse();
        return [.. recent.Select(m => new MessageResponse(m.Id, m.Role, m.Content, m.Timestamp, m.EnhancedPrompt, Attachments: ReadAttachments(m.Attachments)))];
    }

    public async Task<HistorySummary?> GetHistorySummaryAsync(Guid conversationId, Guid userId, CancellationToken ct = default)
    {
        var row = await context.Conversations
            .AsNoTracking()
            .Where(c => c.Id == conversationId && c.UserId == userId && c.HistorySummary != null && c.HistorySummarizedThrough != null)
            .Select(c => new { c.HistorySummary, c.HistorySummarizedThrough })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new HistorySummary(row.HistorySummary!, row.HistorySummarizedThrough!.Value);
    }

    public async Task<IReadOnlyList<MessageResponse>> GetMessagesBeyondWindowAsync(
        Guid conversationId,
        Guid userId,
        DateTime? after,
        int recentMessages,
        int limit,
        CancellationToken ct = default)
    {
        var owned = context.Messages.Where(m => m.ConversationId == conversationId && m.Conversation.UserId == userId);

        var oldestRecent = await owned
            .OrderByDescending(m => m.Timestamp)
            .Skip(recentMessages - 1)
            .Select(m => (DateTime?)m.Timestamp)
            .FirstOrDefaultAsync(ct);

        if (oldestRecent is not { } windowStart)
        {
            return [];
        }

        var since = after ?? DateTime.MinValue;
        var rows = await owned
            .AsNoTracking()
            .Where(m => m.Timestamp < windowStart && m.Timestamp > since)
            .OrderBy(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(ct);

        return [.. rows.Select(ToResponse)];
    }

    public async Task SaveHistorySummaryAsync(Guid conversationId, Guid userId, HistorySummary summary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        await context.Conversations
            .Where(c => c.Id == conversationId && c.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.HistorySummary, summary.Text)
                .SetProperty(c => c.HistorySummarizedThrough, summary.Through), ct);
    }

    /// <summary>
    /// The newest <paramref name="limit"/> of <paramref name="messages"/>, oldest first.
    /// </summary>
    private static async Task<MessagePageResponse> LoadMessagePageAsync(IQueryable<Message> messages, int limit, CancellationToken ct)
    {
        var rows = await messages
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .ThenByDescending(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasEarlier = rows.Count > limit;
        var page = rows.Take(limit).Reverse().Select(ToResponse).ToList();
        return new MessagePageResponse(page, hasEarlier);
    }

    private static MessageResponse ToResponse(Message m)
    {
        return new MessageResponse(
            m.Id,
            m.Role,
            m.Content,
            m.Timestamp,
            m.EnhancedPrompt,
            m.Thought,
            m.ToolCalls != null ? JsonSerializer.Deserialize<List<string>>(m.ToolCalls) : null,
            m.Steps != null ? JsonSerializer.Deserialize<List<MessageStep>>(m.Steps, StepJson) : null,
            ReadAttachments(m.Attachments));
    }

    private static List<MessageAttachment>? ReadAttachments(string? json)
    {
        return json is null ? null : JsonSerializer.Deserialize<List<MessageAttachment>>(json, StepJson);
    }
}
