namespace Sentium.Praxis.Features.Learnings;

internal sealed record CaptureAgentLearningRequest(
    string AgentName,
    string Content,
    string Tags = "",
    Guid? ConversationId = null,
    Guid? UserId = null,
    bool RequestGlobal = false);
