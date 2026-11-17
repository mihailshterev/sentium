namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Binds a conversation to one of the user's workspaces, or unbinds it with <see langword="null"/>.
/// </summary>
internal sealed record UpdateConversationWorkspaceRequest(Guid? WorkspaceId);
