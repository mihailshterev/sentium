namespace Sentium.Memoria.Features.Sync;

internal sealed record VaultSyncResult(int Changed, int Unchanged, int Removed, int NoteCount);
