namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// A chat turn. <paramref name="FileIds"/> are workspace files attached to the latest user message; their text is put
/// in front of the agent for this turn. <paramref name="WorkspaceId"/> applies to ephemeral chats only - a persisted
/// conversation uses the workspace it is bound to.
/// </summary>
internal sealed record ChatRequest(
    Guid? ConversationId,
    string Model,
    IReadOnlyList<ChatMessage> Messages,
    bool Stream = true,
    IReadOnlyList<Guid>? FileIds = null,
    Guid? WorkspaceId = null);
