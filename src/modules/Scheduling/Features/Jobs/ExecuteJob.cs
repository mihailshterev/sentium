using Quartz;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Runs an agent-scheduled script in the sandbox. Failures are logged (the sandbox client records the
/// execution history) and never rethrown: Quartz would otherwise only log them too, and a refire could
/// re-run non-idempotent code. For the same reason the job does not request recovery after a crash.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class ExecuteJob(ISandboxClient sandboxClient, ILogger<ExecuteJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataMap = context.MergedJobDataMap;
        var agentId = dataMap.GetString(ScheduledJobData.AgentId) ?? "System";
        var jobName = dataMap.GetString(ScheduledJobData.JobName) ?? "UnnamedCron";
        var code = dataMap.GetString(ScheduledJobData.Code) ?? string.Empty;
        var language = dataMap.GetString(ScheduledJobData.Language) ?? nameof(ScheduledJobLanguage.Python);

        var correlationId = $"cron-{context.JobDetail.Key.Name}-{context.FireInstanceId}";

        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ScheduledJob"] = context.JobDetail.Key.ToString(),
            ["CorrelationId"] = correlationId
        });

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Scheduled job '{JobName}' for agent '{AgentId}' fired (scheduled {ScheduledFireTime:o}, recovering={Recovering})",
                jobName, agentId, context.ScheduledFireTimeUtc, context.Recovering);
        }

        var req = new SandboxExecutionRequest
        {
            Language = language,
            Code = code,
            AgentId = agentId,
            OriginalUserPrompt = $"Scheduled task '{jobName}' execution for agent '{agentId}'"
        };

        try
        {
            var result = await sandboxClient.ExecuteAsync(req, correlationId, context.CancellationToken);

            if (result.Succeeded)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Scheduled job '{JobName}' completed in {DurationMs} ms", jobName, result.DurationMs);
                }
            }
            else
            {
                logger.LogWarning(
                    "Scheduled job '{JobName}' finished without success. ExitCode={ExitCode} TimedOut={TimedOut} PolicyDenied={PolicyDenied}",
                    jobName, result.ExitCode, result.TimedOut, result.PolicyDenied);
            }
        }
        catch (SandboxRejectedException ex)
        {
            logger.LogWarning("Sandbox rejected scheduled job '{JobName}'. Status: {Status}, Details: {Details}", jobName, ex.StatusCode, ex.Details);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Scheduled job '{JobName}' was interrupted by scheduler shutdown", jobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dispatching scheduled job '{JobName}' to the sandbox", jobName);
        }
    }
}
