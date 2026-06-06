using Quartz;
using Microsoft.Extensions.Logging;
using Sentium.Shared.Constants;
using System.Net.Http.Json;

namespace Sentium.AgentRuntime.Infrastructure.Scheduling;

[DisallowConcurrentExecution]
public sealed class ExecuteJob(IHttpClientFactory httpClientFactory, ILogger<ExecuteJob> logger) : IJob
{
    private sealed record SandboxExecutionRequest(
        string Language,
        string Code,
        string AgentId,
        string? OriginalUserPrompt = null,
        List<object>? FileContext = null
    );

    private sealed record SandboxExecutionResponse(
        bool Succeeded,
        long ExitCode,
        bool TimedOut,
        bool PolicyDenied
    );

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

        var req = new SandboxExecutionRequest(
            Language: language,
            Code: code,
            AgentId: agentId,
            OriginalUserPrompt: $"Scheduled task '{jobName}' execution for agent '{agentId}'"
        );

        try
        {
            var client = httpClientFactory.CreateClient(ServiceNames.Sandbox);

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "sandbox/execute")
            {
                Content = JsonContent.Create(req)
            };

            var correlationId = $"cron-{context.Trigger.Key.Name}";
            requestMessage.Headers.Add(CommonHeaderNames.CorrelationId, correlationId);

            var response = await client.SendAsync(requestMessage, context.CancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SandboxExecutionResponse>(context.CancellationToken);
                if (result is { Succeeded: true })
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
                        jobName, result?.ExitCode, result?.TimedOut, result?.PolicyDenied);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogWarning("[Quartz Warning] Sandbox rejected payload. Status: {Status}, Details: {Details}", response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Quartz Failure] Error dispatching cron execution for job '{JobName}' to Sandbox", jobName);
        }
    }
}
