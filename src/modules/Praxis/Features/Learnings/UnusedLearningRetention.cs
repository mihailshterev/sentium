using Sentium.Infrastructure.Retention;

namespace Sentium.Praxis.Features.Learnings;

/// <summary>
/// Deletes learnings, with their vectors, that no agent has recalled for <c>Praxis:UnusedLearningRetentionDays</c>
/// (counted from capture for one never recalled). Off by default (0): learnings are the household's accumulated
/// knowledge, so retiring them is an explicit choice.
/// </summary>
internal sealed class UnusedLearningRetention(
    IAgentLearningRepository repository,
    IAgentLearningService learnings,
    IConfiguration configuration,
    ILogger<UnusedLearningRetention> logger) : IRetentionSweep
{
    private const string RetentionDaysKey = "Praxis:UnusedLearningRetentionDays";

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var retentionDays = configuration.GetValue(RetentionDaysKey, 0);
        if (retentionDays <= 0)
        {
            return 0;
        }

        var deleted = 0;
        foreach (var id in await repository.GetUnusedSinceAsync(now.AddDays(-retentionDays), ct))
        {
            if (await learnings.DeleteAsync(id, ct))
            {
                deleted++;
            }
        }

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} learnings not recalled in {RetentionDays} days", deleted, retentionDays);
        }

        return deleted;
    }
}
