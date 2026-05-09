namespace Sentium.AgentRuntime.Core.Learnings;

public sealed record AgentLearningResponse(
    Guid Id,
    string AgentName,
    string Content,
    string Tags,
    Guid? ConversationId,
    DateTimeOffset CapturedAt,
    bool IsIngested);

public sealed record CaptureAgentLearningRequest(
    string AgentName,
    string Content,
    string Tags = "",
    Guid? ConversationId = null);

public sealed record UpdateAgentLearningRequest(
    string Content,
    string Tags);

public sealed record AgentLearningStats(
    int TotalLearnings,
    int PendingIngestion,
    IReadOnlyDictionary<string, int> LearningsByAgent);
