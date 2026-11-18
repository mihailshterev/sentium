namespace Sentium.Memoria.Common;

internal static class MemoriaLimits
{
    public const int VaultNameMax = 200;

    public const int PathMax = 1024;

    public const int TitleMax = 512;

    public const int HeadingMax = 512;

    public const int HashLength = 64;

    /// <summary>
    /// Characters of one note's markdown; bigger notes are rejected rather than truncated so the index never
    /// silently disagrees with the vault.
    /// </summary>
    public const int NoteContentMax = 1_000_000;

    public const int NotesPerVaultMax = 20_000;

    public const int NotesPerBatchMax = 50;

    public const int PathsPerBatchMax = 500;

    /// <summary>
    /// Bytes of one sync request: a full batch of large notes, JSON-escaped.
    /// </summary>
    public const long SyncRequestBytesMax = 32L * 1024 * 1024;

    public const int LinksPerNoteMax = 2_000;

    public const int AliasesPerNoteMax = 50;

    public const int TagsPerNoteMax = 100;

    public const int IndexErrorMax = 1_000;
}
