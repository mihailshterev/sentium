namespace Sentium.Praxis.Features.Learnings;

public sealed record AgentLearningResponse(
    Guid Id,
    string AgentName,
    string Content,
    string Tags,
    Guid? ConversationId,
    DateTimeOffset CapturedAt,
    bool IsIngested,
    bool IsGlobal = false);
