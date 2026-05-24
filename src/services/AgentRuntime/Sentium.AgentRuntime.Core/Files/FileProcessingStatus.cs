namespace Sentium.AgentRuntime.Core.Files;

/// <summary>
/// Defines the processing lifecycle stages for project files during ingestion and vectorization.
/// </summary>
/// <remarks>
/// Files flow through this state machine starting at Pending, transitioning through Processing,
/// and concluding at either Completed or Failed depending on the ingestion outcome.
/// </remarks>
public enum FileProcessingStatus
{
    /// <summary>
    /// File has been uploaded but ingestion has not yet begun.
    /// </summary>
    Pending,

    /// <summary>
    /// File is currently being processed (vectorized, ingested into RAG store).
    /// </summary>
    Processing,

    /// <summary>
    /// File has been successfully processed and is available for RAG queries.
    /// </summary>
    Completed,

    /// <summary>
    /// File processing failed. Check logs for details.
    /// </summary>
    Failed
}
