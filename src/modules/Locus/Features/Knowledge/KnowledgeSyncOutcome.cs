namespace Sentium.Locus.Features.Knowledge;

internal enum KnowledgeSyncOutcome
{
    /// <summary>
    /// No entry (already handled).
    /// </summary>
    Skipped,

    /// <summary>
    /// The knowledge base already held this exact content.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The asset's knowledge-base copy was (re)written.
    /// </summary>
    Synced,

    /// <summary>
    /// The asset is gone or private; its knowledge-base copy was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The attempt failed and was rescheduled (or marked failed).
    /// </summary>
    Failed
}
