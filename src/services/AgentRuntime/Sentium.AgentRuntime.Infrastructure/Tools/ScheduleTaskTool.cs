using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Quartz;
using Sentium.AgentRuntime.Core.Scheduling;
using Sentium.AgentRuntime.Core.Tools;
using Sentium.AgentRuntime.Infrastructure.Scheduling;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

public sealed class ScheduleTaskTool(ISchedulerFactory schedulerFactory, ILogger<ScheduleTaskTool> logger) : IAgentTool
{
    public string Name => "schedule_recurring_task";

    public string Description =>
        "Schedules an execution script (Python/NodeJs) to run autonomously on a recurring background timeline. " +
        "CRITICAL ARGS MODELING RULES:\n" +
        "1. 'CronExpression' MUST use 6-field Quartz syntax (e.g., '0 * * * * ?' to run every minute). Never use 5-field Linux crontab format.\n" +
        "2. Provide a descriptive string identifier for 'JobName' (DO NOT use 'task_name').\n" +
        "3. 'language' must be explicitly matched to string values: 'Python' or 'NodeJs'.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } =
    [
        new("jobName", "A descriptive identifier for the scheduled job."),
        new("cronExpression", "6-field Quartz cron expression, e.g. '0 * * * * ?' for every minute. Never 5-field Linux crontab."),
        new("code", "The Python/Node.js script to run on the schedule."),
        new("language", "The script language.", EnumValues: ["Python", "NodeJs"]),
    ];

    private readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record ToolArguments(
        string? AgentId,
        string JobName,
        string CronExpression,
        string? Code,
        string? Script,
        [property: JsonConverter(typeof(JsonStringEnumConverter))]
        ScheduledJobLanguage Language
    );

    public async Task<string> ExecuteAsync(string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Error: No schedule arguments provided. Supply a JSON object with 'JobName', 'CronExpression', 'Code', and 'Language'.";
        }

        try
        {
            var args = JsonSerializer.Deserialize<ToolArguments>(input, options);

            if (args is null || string.IsNullOrWhiteSpace(args.CronExpression) || string.IsNullOrWhiteSpace(args.Code))
            {
                return "Error: Invalid tool arguments. 'CronExpression' and 'Code' are mandatory fields.";
            }

            var finalCode = args.Code ?? args.Script;
            var finalAgentId = args.AgentId ?? "test-agent-default";

            if (!CronExpression.IsValidExpression(args.CronExpression))
            {
                return $"Error: The CRON expression '{args.CronExpression}' is syntactically invalid.";
            }

            var scheduler = await schedulerFactory.GetScheduler(ct);

            var jobKey = new JobKey($"agent-cron-{Guid.NewGuid()}", finalAgentId);
            var triggerKey = new TriggerKey($"agent-trigger-{Guid.NewGuid()}", finalAgentId);

            var jobDetail = JobBuilder.Create<ExecuteJob>()
                .WithIdentity(jobKey)
                .UsingJobData("AgentId", finalAgentId)
                .UsingJobData("JobName", args.JobName)
                .UsingJobData("Code", finalCode)
                .UsingJobData("Language", args.Language.ToString())
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(args.CronExpression)
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger, ct);

            return $"Success: Job '{args.JobName}' successfully saved. It will run autonomously on schedule: {args.CronExpression}.";
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse tool arguments from JSON string payload: {RawInput}", input);
            return "Error: The arguments supplied were not well-formed JSON string tokens matching the target parameters.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected structural error configuring agent cron trigger");
            return $"Error: Internal scheduler system initialization failed. Message: {ex.Message}";
        }
    }
}
