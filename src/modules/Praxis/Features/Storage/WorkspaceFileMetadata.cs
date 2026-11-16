namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// Metadata keys stamped on the knowledge chunks of an ingested workspace file. <see cref="FileId"/> identifies the
/// file's chunks, so re-ingesting replaces them and deleting the file removes them.
/// </summary>
internal static class WorkspaceFileMetadata
{
    public const string FileId = "FileId";
    public const string BlobName = "BlobName";
    public const string Extension = "Extension";
    public const string WorkspaceId = "WorkspaceId";
}
