using Sentium.Core.Constants;
using Sentium.Infrastructure.Storage;

namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// Workspace file content in the <see cref="ResourceNames.WorkspaceBlobs"/> bucket, one object per file named by a
/// GUID, with the filename and workspace as object metadata.
/// </summary>
internal sealed class LocalFileService(IObjectStore objectStore) : ILocalFileService
{
    private const string Bucket = ResourceNames.WorkspaceBlobs;

    public async Task<Guid> UploadToWorkspaceAsync(Stream fileStream, string fileName, Guid? workspaceId, CancellationToken ct = default)
    {
        var blobName = Guid.NewGuid();

        var metadata = new Dictionary<string, string>
        {
            { "FileName", fileName }
        };

        if (workspaceId.HasValue)
        {
            metadata.Add("WorkspaceId", workspaceId.Value.ToString());
        }

        await objectStore.PutAsync(Bucket, blobName.ToString(), fileStream, metadata: metadata, ct: ct);

        return blobName;
    }

    public async Task<Stream> GetFileStreamAsync(Guid blobName, CancellationToken ct = default)
    {
        var stored = await objectStore.OpenReadAsync(Bucket, blobName.ToString(), ct)
            ?? throw new FileNotFoundException($"Workspace file {blobName} is not in storage.");

        return stored.Content;
    }

    public Task DeleteFileAsync(Guid blobName, CancellationToken ct = default)
    {
        return objectStore.DeleteAsync(Bucket, blobName.ToString(), ct);
    }
}
