using Sentium.Core.Common;

namespace Sentium.Praxis.Entities;

internal sealed class Conversation : IUserOwned
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = null!;

    /// <summary>
    /// Whether the title is still the placeholder and should be generated from the conversation's first prompt. Cleared
    /// once a title is generated or the user renames the conversation, so a manual title is never overwritten.
    /// </summary>
    public bool IsTitlePending { get; set; }

    public string Model { get; set; } = null!;

    /// <summary>
    /// The agent this conversation talks to (a built-in specialist or one of the owner's custom agents);
    /// <see langword="null"/> for the general assistant. Fixed at creation, so resuming or reopening the chat
    /// elsewhere keeps the same agent.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// The workspace whose files every turn of this conversation can draw on; <see langword="null"/> for none.
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    public Workspace? Workspace { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the last message was added; <see langword="null"/> for conversations last active before this was tracked,
    /// which sort by <see cref="CreatedAt"/> instead.
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// Listed above every other conversation.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Left out of the conversation list until unarchived, or until a new message brings it back.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Running summary of the messages that have moved out of what the model sees verbatim; see
    /// <c>HistoryWindow</c>. <see langword="null"/> until the conversation is long enough.
    /// </summary>
    public string? HistorySummary { get; set; }

    /// <summary>
    /// Timestamp of the newest message <see cref="HistorySummary"/> covers.
    /// </summary>
    public DateTime? HistorySummarizedThrough { get; set; }

    public ICollection<Message> Messages { get; set; } = [];
}
