namespace Sentium.AgentRuntime.Core.Entities;

public sealed class AgentLearning
{
    public Guid Id { get; set; }

    /// <summary>
    /// The user the capturing agent was acting on behalf of (the learning's origin). Retained even when
    /// <see cref="IsGlobal"/> is <c>true</c> so global learnings stay auditable and revocable.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// When <c>true</c>, the learning is visible to every user's agents (a validated, abstracted,
    /// generalizable pattern). Visibility is decoupled from ownership (<see cref="UserId"/>): a global
    /// learning still records the origin user. Promotion to global is gated by the sanitization and
    /// validation pipeline, never by the caller's role.
    /// </summary>
    public bool IsGlobal { get; set; }

    public string AgentName { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Tags { get; set; } = string.Empty;
    public Guid? ConversationId { get; set; }
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Set to <c>true</c> once the learning has been embedded and stored in the vector store.
    /// The <see cref="AgentLearningIngestionWorker"/> flips this flag after successful ingestion.
    /// </summary>
    public bool IsIngested { get; set; }
}
