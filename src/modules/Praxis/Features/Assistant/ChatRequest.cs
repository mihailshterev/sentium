namespace Sentium.Praxis.Features.Assistant;

internal sealed record ChatRequest(
    Guid? ConversationId,
    string Model,
    IReadOnlyList<ChatMessage> Messages,
    bool Stream = true);
