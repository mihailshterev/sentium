using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;
using Quartz;
using Quartz.Impl.Matchers;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Schedules a script to run in the sandbox on a recurring Quartz cron timeline.
/// </summary>
/// <remarks>
/// Recurring code execution is at least as risky as a one-off <c>execute_code_sandbox</c> call (it keeps running
/// unattended), so it carries the same High risk / human-approval policy and is classified as code execution for
/// Sentinel. The owning user and the agent come from the run context, never from the model's arguments.
/// </remarks>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.High, RequiresApproval = true, Resource = ToolResource.Code, Action = ToolAction.Execute)]
public sealed class ScheduleTaskTool(
    ISchedulerFactory schedulerFactory,
    IPdpContextAccessor pdpContext,
    ILogger<ScheduleTaskTool> logger) : IAgentTool
{
    public const string ToolName = "schedule_recurring_task";

    private const int IntervalSamples = 50;

    public AIFunction CreateFunction()
    {
        return AgentToolFunction.Create(
            this,
            ScheduleAsync,
            ToolName,
            "Schedules a Python or Node.js script to run autonomously on a recurring background timeline. " +
            "The cron expression MUST use 6-field Quartz syntax (e.g. '0 0 * * * ?' to run every hour) - never the 5-field Linux crontab format. " +
            $"Times are UTC, and runs must be at least {SchedulerDefaults.MinimumInterval.TotalMinutes:0} minute(s) apart.");
    }

    public async Task<string> ScheduleAsync(
        [Description("A descriptive identifier for the scheduled job.")] string jobName,
        [Description("6-field Quartz cron expression in UTC, e.g. '0 0 * * * ?' for every hour. Never 5-field Linux crontab.")] string cronExpression,
        [Description("The Python/Node.js script to run on the schedule.")] string code,
        [Description("The script language.")] ScheduledJobLanguage language = ScheduledJobLanguage.Python,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cronExpression) || string.IsNullOrWhiteSpace(code))
        {
            return "Error: Invalid tool arguments. 'cronExpression' and 'code' are mandatory.";
        }

        if (pdpContext.UserId is not { } userId || userId == Guid.Empty)
        {
            return "Error: Scheduled tasks need a signed-in user, and none is associated with this run.";
        }

        if (!CronExpression.IsValidExpression(cronExpression))
        {
            return $"Error: The CRON expression '{cronExpression}' is syntactically invalid. Use 6-field Quartz syntax.";
        }

        var cron = new CronExpression(cronExpression) { TimeZone = TimeZoneInfo.Utc };
        var firstFire = cron.GetNextValidTimeAfter(DateTimeOffset.UtcNow);

        if (firstFire is null)
        {
            return $"Error: The CRON expression '{cronExpression}' never fires in the future.";
        }

        if (GetShortestInterval(cron, firstFire.Value) is { } shortest && shortest < SchedulerDefaults.MinimumInterval)
        {
            return $"Error: The CRON expression '{cronExpression}' fires every {shortest.TotalSeconds:0} second(s); " +
                   $"scheduled tasks must be at least {SchedulerDefaults.MinimumInterval.TotalMinutes:0} minute(s) apart.";
        }

        var name = string.IsNullOrWhiteSpace(jobName) ? "UnnamedTask" : jobName.Trim();
        var agentId = AgentToolFunction.CurrentAgentName;
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
                .WithDescription(name)
                .UsingJobData(ScheduledJobData.AgentId, agentId)
                .UsingJobData(ScheduledJobData.JobName, name)
                .UsingJobData(ScheduledJobData.Code, code)
                .UsingJobData(ScheduledJobData.Language, language.ToString())
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(new TriggerKey($"agent-trigger-{Guid.NewGuid()}", group))
                .WithCronSchedule(cronExpression, schedule => schedule
                    .InTimeZone(TimeZoneInfo.Utc)
                    .WithMisfireHandlingInstructionFireAndProceed())
                .Build();

            var firstRun = await scheduler.ScheduleJob(jobDetail, trigger, ct);

            logger.LogInformation(
                "Agent '{AgentId}' scheduled job {JobKey} ('{JobName}') with cron '{Cron}' for user {UserId}",
                agentId, jobDetail.Key, name, cronExpression, userId);

            return $"Success: Job '{name}' was saved and will run on schedule '{cronExpression}' (UTC). " +
                   $"First run: {firstRun.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to schedule job '{JobName}' for user {UserId}", name, userId);
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
