namespace Sentium.Memoria.Features.Notes;

/// <param name="Tag">The note's first tag, for colouring; <see langword="null"/> when untagged.</param>
internal sealed record VaultGraphNodeDto(Guid Id, string Title, string Path, string Folder, string? Tag, int Degree);
