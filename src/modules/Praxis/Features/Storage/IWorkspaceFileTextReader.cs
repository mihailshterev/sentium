namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// Reads a stored workspace file as plain text (PDF and DOCX extracted), for agent reads, injected context and
/// previews.
/// </summary>
internal interface IWorkspaceFileTextReader
{
    /// <exception cref="FileNotFoundException">Thrown when the blob is missing from storage.</exception>
    /// <exception cref="InvalidDataException">Thrown when a document cannot be parsed.</exception>
    Task<string> ReadAsync(Guid blobName, string extension, CancellationToken ct = default);
}
