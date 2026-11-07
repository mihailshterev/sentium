namespace Sentium.Praxis.Dtos;

/// <summary>
/// Creates a conversation. Without a <paramref name="Title"/> the conversation starts as
/// <see cref="ConversationTitles.Placeholder"/> and is titled from its first prompt.
/// </summary>
public sealed record CreateConversationRequest(
    string? Title,
    string Model);

/// <summary>
/// Renames a conversation or a workflow run.
/// </summary>
public sealed record UpdateTitleRequest(string Title);

public static class ConversationTitles
{
    /// <summary>Title of a conversation that has not been titled yet.</summary>
    public const string Placeholder = "New chat";

    /// <summary>Longest stored title (conversations and workflow runs).</summary>
    public const int MaxLength = 255;
}
