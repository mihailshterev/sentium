namespace Sentium.Backup.Parts;

/// <summary>
/// Maps a numbered tar entry back to the blob it came from. Entries are numbered rather than named after the
/// blob, so a blob name can never become a path on restore.
/// </summary>
internal sealed record BlobIndexEntry(
    string Entry,
    string Container,
    string Name,
    long SizeBytes,
    string? ContentType,
    IDictionary<string, string>? Metadata);
