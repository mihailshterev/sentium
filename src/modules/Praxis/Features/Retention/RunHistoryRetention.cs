using Microsoft.EntityFrameworkCore;
using Sentium.Infrastructure.Retention;
using Sentium.Praxis.Data;

namespace Sentium.Praxis.Features.Retention;

/// <summary>
/// Deletes sandbox execution records older than <c>Praxis:ExecutionLogRetentionDays</c> (default 90; each holds the
/// code, its file context and output) and workflow runs older than <c>Praxis:WorkflowRunRetentionDays</c> (default 180).
/// Conversations are the user's own content and are never expired.
/// </summary>
internal sealed class RunHistoryRetention(PraxisDbContext db, IConfiguration configuration, ILogger<RunHistoryRetention> logger) : IRetentionSweep
{
    private const string ExecutionLogRetentionDaysKey = "Praxis:ExecutionLogRetentionDays";
    private const string WorkflowRunRetentionDaysKey = "Praxis:WorkflowRunRetentionDays";
    private const int DefaultExecutionLogRetentionDays = 90;
    private const int DefaultWorkflowRunRetentionDays = 180;
    private const int MinRetentionDays = 1;

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct)
    {
        var executionDays = RetentionDays(ExecutionLogRetentionDaysKey, DefaultExecutionLogRetentionDays);
        var executionCutoff = now.AddDays(-executionDays);
        var executions = await db.ExecutionLogs
            .IgnoreQueryFilters()
            .Where(e => e.ExecutedAt < executionCutoff)
            .ExecuteDeleteAsync(ct);

        var runDays = RetentionDays(WorkflowRunRetentionDaysKey, DefaultWorkflowRunRetentionDays);
        var runCutoff = now.AddDays(-runDays).UtcDateTime;
        var runs = await db.WorkflowRuns
            .IgnoreQueryFilters()
            .Where(r => r.StartedAt < runCutoff)
            .ExecuteDeleteAsync(ct);

        if (executions + runs > 0)
        {
            logger.LogInformation(
                "Deleted {Executions} sandbox executions older than {ExecutionDays} days and {Runs} workflow runs older than {RunDays} days",
                executions, executionDays, runs, runDays);
        }

        return executions + runs;
    }

    private int RetentionDays(string key, int defaultDays) => Math.Max(MinRetentionDays, configuration.GetValue(key, defaultDays));
}
