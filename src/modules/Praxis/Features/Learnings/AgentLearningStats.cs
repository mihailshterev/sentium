namespace Sentium.Praxis.Features.Learnings;

internal sealed record AgentLearningStats(
    int TotalLearnings,
    int PendingIngestion,
    int GlobalLearnings,
    IReadOnlyDictionary<string, int> LearningsByAgent);
