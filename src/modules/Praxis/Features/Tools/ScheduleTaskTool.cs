using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;
using Sentium.Praxis.Features.Scheduling;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Lets an agent register a recurring sandbox script. Scheduling code is at least as dangerous as running it
/// once (it keeps running unattended), so the tool carries the same High-risk approval gate as
/// <see cref="CodeExecutionSandboxTool"/>. The owner and agent come from the run context, never from the model.
/// </summary>
[AgentToolPolicy(
    AllowedAgents = [],
    RiskLevel = ToolRiskLevel.High,
    RequiresApproval = true)]
public sealed class ScheduleTaskTool(
    ISchedulerFactory schedulerFactory,
    IPdpContextAccessor pdpContext,
    ILogger<ScheduleTaskTool> logger) : IAgentTool
{
    private const int IntervalSamples = 50;

    public string Name => "schedule_recurring_task";

    public string Description =>
        "Schedules an execution script (Python/NodeJs) to run autonomously on a recurring background timeline. " +
        "CRITICAL ARGS MODELING RULES:\n" +
        "1. 'CronExpression' MUST use 6-field Quartz syntax (e.g., '0 0 * * * ?' to run every hour). Never use 5-field Linux crontab format. " +
        $"Times are UTC, and runs must be at least {SchedulerDefaults.MinimumInterval.TotalMinutes:0} minute(s) apart.\n" +
        "2. Provide a descriptive string identifier for 'JobName' (DO NOT use 'task_name').\n" +
        "3. 'language' must be explicitly matched to string values: 'Python' or 'NodeJs'.";

    public IReadOnlyList<AgentToolParameter> Parameters { get; } =
    [
        new("jobName", "A descriptive identifier for the scheduled job."),
        new("cronExpression", "6-field Quartz cron expression in UTC, e.g. '0 0 * * * ?' for every hour. Never 5-field Linux crontab."),
        new("code", "The Python/Node.js script to run on the schedule."),
        new("language", "The script language.", EnumValues: ["Python", "NodeJs"]),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record ToolArguments(
        string? JobName,
        string? CronExpression,
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

        ToolArguments? args;
        try
        {
            args = JsonSerializer.Deserialize<ToolArguments>(input, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse schedule_recurring_task arguments");
            return "Error: The arguments supplied were not well-formed JSON matching the tool parameters.";
        }

        var code = args?.Code ?? args?.Script;

        if (args is null || string.IsNullOrWhiteSpace(args.CronExpression) || string.IsNullOrWhiteSpace(code))
        {
            return "Error: Invalid tool arguments. 'CronExpression' and 'Code' are mandatory fields.";
        }

        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return "Error: Scheduled tasks need a signed-in user, and none is associated with this run.";
        }

        if (!CronExpression.IsValidExpression(args.CronExpression))
        {
            return $"Error: The CRON expression '{args.CronExpression}' is syntactically invalid.";
        }

        var cron = new CronExpression(args.CronExpression) { TimeZone = TimeZoneInfo.Utc };
        var now = DateTimeOffset.UtcNow;
        var firstFire = cron.GetNextValidTimeAfter(now);

        if (firstFire is null)
        {
            return $"Error: The CRON expression '{args.CronExpression}' never fires in the future.";
        }

        if (GetShortestInterval(cron, firstFire.Value) is { } shortest && shortest < SchedulerDefaults.MinimumInterval)
        {
            return $"Error: The CRON expression '{args.CronExpression}' fires every {shortest.TotalSeconds:0} second(s); " +
                   $"scheduled tasks must be at least {SchedulerDefaults.MinimumInterval.TotalMinutes:0} minute(s) apart.";
        }

        var jobName = string.IsNullOrWhiteSpace(args.JobName) ? "UnnamedTask" : args.JobName.Trim();
        var agentId = string.IsNullOrWhiteSpace(pdpContext.AgentName) ? "Agent" : pdpContext.AgentName;
        var group = SchedulerDefaults.GroupFor(userId);

        try
        {
            var scheduler = await schedulerFactory.GetScheduler(ct);

            var owned = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group), ct);
            if (owned.Count >= SchedulerDefaults.MaxJobsPerUser)
            {
                return $"Error: You already have {owned.Count} scheduled tasks (limit {SchedulerDefaults.MaxJobsPerUser}). " +
                       "Ask the user to delete one from the Scheduler page first.";
            }

            var jobDetail = JobBuilder.Create<ExecuteJob>()
                .WithIdentity(new JobKey($"agent-cron-{Guid.NewGuid()}", group))
                .WithDescription(jobName)
                .UsingJobData(ScheduledJobData.AgentId, agentId)
                .UsingJobData(ScheduledJobData.JobName, jobName)
                .UsingJobData(ScheduledJobData.Code, code)
                .UsingJobData(ScheduledJobData.Language, args.Language.ToString())
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(new TriggerKey($"agent-trigger-{Guid.NewGuid()}", group))
                .WithCronSchedule(args.CronExpression, schedule => schedule
                    .InTimeZone(TimeZoneInfo.Utc)
                    .WithMisfireHandlingInstructionFireAndProceed())
                .Build();

            var firstRun = await scheduler.ScheduleJob(jobDetail, trigger, ct);

            logger.LogInformation(
                "Agent '{AgentId}' scheduled job {JobKey} ('{JobName}') with cron '{Cron}' for user {UserId}",
                agentId, jobDetail.Key, jobName, args.CronExpression, userId);

            return $"Success: Job '{jobName}' was saved and will run on schedule '{args.CronExpression}' (UTC). " +
                   $"First run: {firstRun.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to schedule job '{JobName}' for user {UserId}", jobName, userId);
            return "Error: The scheduler could not save the task. Try again later.";
        }
    }

    private static TimeSpan? GetShortestInterval(CronExpression cron, DateTimeOffset firstFire)
    {
        TimeSpan? shortest = null;
        var previous = firstFire;

        for (var i = 0; i < IntervalSamples; i++)
        {
            if (cron.GetNextValidTimeAfter(previous) is not { } next)
            {
                break;
            }

            var gap = next - previous;
            if (shortest is null || gap < shortest)
            {
                shortest = gap;
            }

            previous = next;
        }

        return shortest;
    }
}
