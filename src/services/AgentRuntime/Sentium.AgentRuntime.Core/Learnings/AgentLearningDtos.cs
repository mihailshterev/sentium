namespace Sentium.AgentRuntime.Core.Learnings;

public sealed record AgentLearningResponse(
    Guid Id,
    string AgentName,
    string Content,
    string Tags,
    Guid? ConversationId,
    DateTimeOffset CapturedAt,
    bool IsIngested,
    bool IsGlobal = false);

public sealed record CaptureAgentLearningRequest(
    string AgentName,
    string Content,
    string Tags = "",
    Guid? ConversationId = null,
    Guid? UserId = null,
    bool RequestGlobal = false);

public sealed record UpdateAgentLearningRequest(
    string Content,
    string Tags);

public sealed record AgentLearningStats(
    int TotalLearnings,
    int PendingIngestion,
    int GlobalLearnings,
    IReadOnlyDictionary<string, int> LearningsByAgent);
