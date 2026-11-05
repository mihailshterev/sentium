using Quartz;
using Microsoft.Extensions.Logging;
using Sentium.Praxis.Features.CodeExecution;

namespace Sentium.Praxis.Features.Scheduling;

[DisallowConcurrentExecution]
public sealed class ExecuteJob(ISandboxClient sandboxClient, ILogger<ExecuteJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dataMap = context.MergedJobDataMap;
        var agentId = dataMap.GetString("AgentId") ?? "System";
        var jobName = dataMap.GetString("JobName") ?? "UnnamedCron";
        var code = dataMap.GetString("Code") ?? string.Empty;
        var language = dataMap.GetString("Language") ?? "Python";

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("[Quartz Triggered] Running job '{JobName}' for Agent '{AgentId}'", jobName, agentId);
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
            var correlationId = $"cron-{context.Trigger.Key.Name}";

            var result = await sandboxClient.ExecuteAsync(req, correlationId, context.CancellationToken);

            if (result.Succeeded)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("[Quartz Success] Sandbox execution completed for job '{JobName}'", jobName);
                }
            }
            else
            {
                logger.LogWarning(
                    "[Quartz Warning] Sandbox execution finished but did not succeed for job '{JobName}'. ExitCode={ExitCode} TimedOut={TimedOut} PolicyDenied={PolicyDenied}",
                    jobName, result.ExitCode, result.TimedOut, result.PolicyDenied);
            }
        }
        catch (SandboxRejectedException ex)
        {
            logger.LogWarning("[Quartz Warning] Sandbox rejected payload. Status: {Status}, Details: {Details}", ex.StatusCode, ex.Details);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Quartz Failure] Error dispatching cron execution for job '{JobName}' to Sandbox", jobName);
        }
    }
}
