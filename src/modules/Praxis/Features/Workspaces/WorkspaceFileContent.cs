namespace Sentium.Praxis.Features.Workspaces;

/// <summary>
/// An open stream over a file's original bytes; the caller owns and disposes <see cref="Content"/>.
/// </summary>
internal sealed record WorkspaceFileContent(string FileName, string Extension, Stream Content);
