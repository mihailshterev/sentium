using Sentium.Praxis.Features.Conversations;

namespace Sentium.Praxis.Features.Workspaces;

/// <summary>
/// Renders what an agent should know about a workspace and the files attached to a request: the workspace's file
/// manifest and the attached files' text, within a character budget. Shared by chat (through
/// <see cref="WorkspaceContextProvider"/>) and workflow runs.
/// </summary>
/// <remarks>
/// Every lookup filters on <c>userId</c> explicitly: workflow runs execute in a system scope that bypasses the
/// DbContext's per-user query filter.
/// </remarks>
internal interface IWorkspaceContextBuilder
{
    /// <summary>
    /// The files among <paramref name="fileIds"/> that <paramref name="userId"/> owns, in the requested order; unknown
    /// or foreign ids are dropped.
    /// </summary>
    Task<IReadOnlyList<MessageAttachment>> ResolveAttachmentsAsync(Guid userId, IEnumerable<Guid> fileIds, CancellationToken ct = default);

    /// <summary>
    /// The context block, or <see langword="null"/> when there is neither an owned workspace nor an owned attached file.
    /// Attached files' text shares <paramref name="maxContentChars"/>; a file that does not fit is cut with a note on
    /// how to read on.
    /// </summary>
    Task<string?> BuildAsync(Guid userId, Guid? workspaceId, IReadOnlyList<Guid> fileIds, int maxContentChars, CancellationToken ct = default);
}
