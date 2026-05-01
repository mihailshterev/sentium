using AgentRuntime.Core.Conversations;
using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.Entities;
using AgentRuntime.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AgentRuntime.Infrastructure.Conversations;

public sealed class ConversationManager(AgentRuntimeDbContext context) : IConversationManager
{
    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken ct = default)
    {
        return await context.Conversations
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ConversationSummary(c.Id, c.Title, c.Model, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ConversationResponse> GetConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var conv = await context.Conversations
            .AsNoTracking()
            .Include(c => c.Messages.OrderBy(m => m.Timestamp))
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        return new ConversationResponse(
            conv.Id,
            conv.Title,
            conv.Model,
            conv.CreatedAt,
            conv.Messages.Select(m => new MessageResponse(
                m.Id, m.Role, m.Content, m.Timestamp,
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

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken ct = default)
    {
        var affected = await context.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteDeleteAsync(ct);

        if (affected == 0)
        {
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");
        }
    }

    public async Task AddMessageAsync(Guid conversationId, string role, string content, string? thought = null, IReadOnlyList<string>? toolCalls = null, CancellationToken ct = default)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            Thought = thought,
            ToolCalls = toolCalls != null ? JsonSerializer.Serialize(toolCalls) : null,
            Timestamp = DateTime.UtcNow
        };

        context.Messages.Add(message);
        await context.SaveChangesAsync(ct);
    }
}
