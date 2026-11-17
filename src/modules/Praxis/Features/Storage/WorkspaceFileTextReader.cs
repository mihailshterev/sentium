using Sentium.Praxis.Features.Files;

namespace Sentium.Praxis.Features.Storage;

internal sealed class WorkspaceFileTextReader(ILocalFileService fileService, IFileTextExtractor extractor) : IWorkspaceFileTextReader
{
    public async Task<string> ReadAsync(Guid blobName, string extension, CancellationToken ct = default)
    {
        await using var stream = await fileService.GetFileStreamAsync(blobName, ct);
        return await extractor.ExtractAsync(stream, extension, ct);
    }
}
