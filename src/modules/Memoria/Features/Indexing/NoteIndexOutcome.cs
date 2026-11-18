namespace Sentium.Memoria.Features.Indexing;

internal enum NoteIndexOutcome
{
    /// <summary>
    /// No entry (already handled).
    /// </summary>
    Skipped,

    /// <summary>
    /// The knowledge base already held this exact note.
    /// </summary>
    Unchanged,

    Indexed,

    /// <summary>
    /// The note is gone; its chunks were removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The attempt failed and was rescheduled (or marked failed).
    /// </summary>
    Failed
}
