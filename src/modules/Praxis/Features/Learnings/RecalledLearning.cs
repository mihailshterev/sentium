namespace Sentium.Praxis.Features.Learnings;

public sealed record RecalledLearning(
    string Content,
    float Score,
    string AgentName);
