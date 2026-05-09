namespace Sentium.AgentRuntime.Core.Entities;

public sealed class AgentLearning
{
    public Guid Id { get; set; }
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
