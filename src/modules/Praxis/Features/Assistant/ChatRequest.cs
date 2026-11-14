namespace Sentium.Praxis.Features.Assistant;

public sealed record ChatRequest(
    Guid? ConversationId,
    string Model,
    IReadOnlyList<ChatMessage> Messages,
    bool Stream = true);
