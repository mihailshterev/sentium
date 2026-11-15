namespace Sentium.Backup.Tools;

/// <summary>
/// Ends a backup run or a restore with a message meant for the operator.
/// </summary>
internal sealed class BackupFailedException : Exception
{
    public BackupFailedException()
    {
    }

    public BackupFailedException(string message) : base(message)
    {
    }

    public BackupFailedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
