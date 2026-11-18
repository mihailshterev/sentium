namespace Sentium.Memoria.Features.Sync;

internal sealed record RemoveNotesRequest(IReadOnlyList<string> Paths);
