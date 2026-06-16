using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Entities;
using Sentium.AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Sentium.AgentRuntime.Infrastructure.Conversations;

public sealed class ConversationRepository(AgentRuntimeDbContext context) : IConversationRepository
{
    public async Task<(IReadOnlyList<ConversationSummary> Items, int TotalCount)> GetConversationsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Conversations.AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationSummary(c.Id, c.Title, c.Model, c.CreatedAt))
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
                m.ToolCalls != null ? JsonSerializer.Deserialize<List<string>>(m.ToolCalls) : null)).ToList());
    }

    public async Task<ConversationSummary> CreateConversationAsync(CreateConversationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Model = request.Model,
            CreatedAt = DateTime.UtcNow
        };

        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(ct);

        return new ConversationSummary(conversation.Id, conversation.Title, conversation.Model, conversation.CreatedAt);
    }

    public async Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteDeleteAsync(ct);

        return affected > 0;
    }

    public async Task AddMessageAsync(Guid conversationId, string role, string content, string? enhancedPrompt = null, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            EnhancedPrompt = enhancedPrompt,
            Thought = thought,
            ToolCalls = toolCalls != null ? JsonSerializer.Serialize(toolCalls) : null,
            Timestamp = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(ct);
    }
}
