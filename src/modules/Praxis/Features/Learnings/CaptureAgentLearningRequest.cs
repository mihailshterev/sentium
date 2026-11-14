namespace Sentium.Praxis.Features.Learnings;

public sealed record CaptureAgentLearningRequest(
    string AgentName,
    string Content,
    string Tags = "",
    Guid? ConversationId = null,
    Guid? UserId = null,
    bool RequestGlobal = false);
