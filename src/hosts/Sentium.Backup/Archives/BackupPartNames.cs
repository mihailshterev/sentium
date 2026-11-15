namespace Sentium.Backup.Archives;

internal static class BackupPartNames
{
    /// <summary>
    /// <c>pg_dump -Fc</c> of the whole <c>sentiumdb</c> database.
    /// </summary>
    public const string Database = "database.dump";

    /// <summary>
    /// A tar of every blob in every container, plus an index of their names and properties.
    /// </summary>
    public const string Blobs = "blobs.tar";

    /// <summary>
    /// A tar of the Sentium.Web data-protection key ring.
    /// </summary>
    public const string Keys = "keys.tar";

    public static readonly IReadOnlyList<string> All = [Database, Blobs, Keys];
}
