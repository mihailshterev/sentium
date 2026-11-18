namespace Sentium.Memoria.Features.Sync;

/// <param name="IsThisDevice">Whether the calling Device holds the vault and may sync it.</param>
internal sealed record DeviceVaultDto(Guid Id, string Name, bool IsThisDevice, int NoteCount, DateTimeOffset? LastSyncAt);
