namespace Sentium.Memoria.Features.Sync;

/// <param name="Hash">Lower-case hex SHA-256 of <paramref name="Content"/> as UTF-8; checked by the server.</param>
internal sealed record NoteUpload(string Path, string Hash, string Content);
