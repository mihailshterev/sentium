using System.Text.Json.Serialization;

namespace Sentium.Codex.Contracts.Models;

/// <summary>
/// Identifies the origin of ingested content, enabling source-aware retrieval
/// and allowing the knowledge base to be filtered by data type.
/// Extend this enum as new ingestion sources are onboarded.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IngestionSourceType
{
    /// <summary>
    /// Anomaly and alert logs from the Sentinel service.
    /// </summary>
    SentinelLogs,

    /// <summary>
    /// Health and threshold alerts from the Vigil service.
    /// </summary>
    VigilAlerts,

    /// <summary>
    /// Context or notes provided interactively by the user.
    /// </summary>
    UserInput,

    /// <summary>
    /// Any other programmatic ingestion source.
    /// </summary>
    Custom,

    /// <summary>
    /// Ingestion source is a file uploaded by the user.
    /// </summary>
    File,

    /// <summary>
    /// Knowledge captured by an agent during interactions for self-improvement.
    /// </summary>
    AgentLearning
}
