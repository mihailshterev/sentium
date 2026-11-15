namespace Sentium.Backup.Archives;

/// <summary>
/// A finished archive at the destination. An archive only gets its final name after it was verified, so every
/// <see cref="BackupArchive"/> is a verified backup.
/// </summary>
internal sealed record BackupArchive(string Name, DateTimeOffset CreatedAt, long SizeBytes, bool Encrypted);
