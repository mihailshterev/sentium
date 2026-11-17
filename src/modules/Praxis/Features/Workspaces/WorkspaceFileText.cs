namespace Sentium.Praxis.Features.Workspaces;

/// <summary>
/// The extracted text of a file for previewing, cut at the requested length when <see cref="Truncated"/>.
/// </summary>
internal sealed record WorkspaceFileText(Guid FileId, string FileName, string Text, bool Truncated, int TotalChars);
