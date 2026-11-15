namespace Sentium.Vigil.Features.Backups;

internal sealed record BackupArchiveDto(string Name, DateTimeOffset CreatedAt, long SizeBytes, bool Encrypted);
