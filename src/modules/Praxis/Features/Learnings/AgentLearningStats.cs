namespace Sentium.Praxis.Features.Learnings;

public sealed record AgentLearningStats(
    int TotalLearnings,
    int PendingIngestion,
    int GlobalLearnings,
    IReadOnlyDictionary<string, int> LearningsByAgent);
