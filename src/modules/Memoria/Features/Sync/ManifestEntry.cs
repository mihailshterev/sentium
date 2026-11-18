namespace Sentium.Memoria.Features.Sync;

/// <param name="Hash">Lower-case hex SHA-256 of the file's UTF-8 bytes.</param>
internal sealed record ManifestEntry(string Path, string Hash);
