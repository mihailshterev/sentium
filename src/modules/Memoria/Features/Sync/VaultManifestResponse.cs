namespace Sentium.Memoria.Features.Sync;

/// <param name="Upload">Paths whose content Sentium lacks or holds in another version; send them next.</param>
internal sealed record VaultManifestResponse(IReadOnlyList<string> Upload, int Removed, int NoteCount);
