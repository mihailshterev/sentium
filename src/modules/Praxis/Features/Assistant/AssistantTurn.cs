using Sentium.Praxis.Features.Conversations;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// One accepted assistant turn, run by <see cref="AssistantTurnProcessor"/> independently of the request that started
/// it. Its updates go to the relay stream <see cref="TurnId"/>, so a client can drop and resume following it.
/// </summary>
internal sealed record AssistantTurn
{
    public required string TurnId { get; init; }

    public required Guid UserId { get; init; }

    public required string CorrelationId { get; init; }

    public required string Model { get; init; }

    public Guid? ConversationId { get; init; }

    /// <summary>
    /// The latest user message as the agent gets it (a stand-in prompt when only files were attached);
    /// <see langword="null"/> when the request carried none.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// What the user asked, for policy checks; for a resumed run, the prompt of the run that paused.
    /// </summary>
    public string OriginalUserPrompt { get; init; } = string.Empty;

    /// <summary>
    /// The client-held history of an ephemeral chat; a persisted conversation loads its own and uses only
    /// <see cref="Prompt"/>.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    public IReadOnlyList<MessageAttachment> Attachments { get; init; } = [];

    /// <summary>
    /// The workspace of an ephemeral chat; a persisted conversation uses the workspace it is bound to.
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Set when the turn resumes a run that paused on a tool approval.
    /// </summary>
    public ApprovalDecision? Approval { get; init; }
}
