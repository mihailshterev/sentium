namespace Sentium.Memoria.Features.Links;

internal sealed record NoteKey(Guid Id, string PathKey, IReadOnlyList<string> AliasKeys);
