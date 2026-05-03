namespace Sentium.AgentRuntime.Core.Dtos;

public sealed record ChatRequest(
    Guid? ConversationId,
    string Model,
    IReadOnlyList<ChatMessage> Messages,
    bool Stream = true);

public sealed record ChatMessage(
    string Role,
    string Content,
    IReadOnlyList<string>? Images = null);
