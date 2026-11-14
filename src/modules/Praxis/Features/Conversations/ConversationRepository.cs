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

    public async Task<(IReadOnlyList<ConversationSummary> Items, int TotalCount)> GetConversationsAsync(int page, int pageSize, string? search = null, CancellationToken ct = default)
    {
        var query = context.Conversations.AsNoTracking();
        if (TextSearch.Normalize(search) is { } term)
        {
            var pattern = TextSearch.ToContainsPattern(term);
            query = query.Where(c => EF.Functions.ILike(c.Title, pattern, TextSearch.EscapeCharacter));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationSummary(c.Id, c.Title, c.Model, c.CreatedAt, c.AgentName))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ConversationResponse?> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conv = await context.Conversations
            .AsNoTracking()
            .Include(c => c.Messages.OrderBy(m => m.Timestamp))
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conv is null)
        {
            return null;
        }

        return new ConversationResponse(
            conv.Id,
            conv.Title,
            conv.Model,
            conv.CreatedAt,
            conv.Messages.Select(m => new MessageResponse(
                m.Id, m.Role, m.Content, m.Timestamp,
                m.EnhancedPrompt,
                m.Thought,
                m.ToolCalls != null ? JsonSerializer.Deserialize<List<string>>(m.ToolCalls) : null,
                m.Steps != null ? JsonSerializer.Deserialize<List<MessageStep>>(m.Steps, StepJson) : null)).ToList(),
            conv.AgentName);
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
            CreatedAt = DateTime.UtcNow
        };

        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(ct);

        return new ConversationSummary(conversation.Id, conversation.Title, conversation.Model, conversation.CreatedAt, conversation.AgentName);
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

    public async Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, IReadOnlyList<MessageStep>? steps = null, CancellationToken ct = default)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            EnhancedPrompt = enhancedPrompt,
            Steps = steps is { Count: > 0 } ? JsonSerializer.Serialize(steps, StepJson) : null,
            Timestamp = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(ct);
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
            .Select(m => new MessageResponse(m.Id, m.Role, m.Content, m.Timestamp, m.EnhancedPrompt, null, null))
            .ToListAsync(ct);

        recent.Reverse();
        return recent;
    }
}
