namespace Sentium.Backup.Archives;

/// <summary>
/// One file inside an archive, with the size and lowercase hex SHA-256 it had when it was written.
/// </summary>
internal sealed record BackupPart(string Name, long SizeBytes, string Sha256);
