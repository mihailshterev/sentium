namespace Sentium.Praxis.Features.Files;

/// <summary>
/// Turns stored workspace file content into plain text for ingestion, agent reads and previews: PDF and DOCX through
/// their parsers, everything else decoded as text (UTF-8 unless a byte-order mark says otherwise).
/// </summary>
internal interface IFileTextExtractor
{
    /// <exception cref="InvalidDataException">Thrown when a PDF or DOCX document cannot be parsed.</exception>
    Task<string> ExtractAsync(Stream content, string extension, CancellationToken ct = default);
}
