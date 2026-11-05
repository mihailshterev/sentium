using System.ComponentModel;
using Microsoft.Extensions.AI;
using Quartz;
using Sentium.Praxis.Features.Scheduling;
using Sentium.Praxis.Features.Tools.Attributes;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Schedules a script to run in the sandbox on a recurring Quartz cron timeline.
/// </summary>
/// <remarks>
/// Recurring code execution is at least as risky as a one-off <c>execute_code_sandbox</c> call, so it carries the same
/// High risk / human-approval policy and is classified as code execution for Sentinel.
/// </remarks>
[AgentToolPolicy(RiskLevel = ToolRiskLevel.High, RequiresApproval = true, Resource = ToolResource.Code, Action = ToolAction.Execute)]
public sealed class ScheduleTaskTool(ISchedulerFactory schedulerFactory, ILogger<ScheduleTaskTool> logger) : IAgentTool
{
    public AIFunction CreateFunction() => AgentToolFunction.Create(
        this,
        ScheduleAsync,
        ToolNames.ScheduleRecurringTask,
        "Schedules a Python or Node.js script to run autonomously on a recurring background timeline. " +
        "The cron expression MUST use 6-field Quartz syntax (e.g. '0 * * * * ?' to run every minute) - never the 5-field Linux crontab format.");

    public async Task<string> ScheduleAsync(
        [Description("A descriptive identifier for the scheduled job.")] string jobName,
        [Description("6-field Quartz cron expression, e.g. '0 * * * * ?' for every minute. Never 5-field Linux crontab.")] string cronExpression,
        [Description("The Python/Node.js script to run on the schedule.")] string code,
        [Description("The script language.")] ScheduledJobLanguage language = ScheduledJobLanguage.Python,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cronExpression) || string.IsNullOrWhiteSpace(code))
        {
            return "Error: Invalid tool arguments. 'cronExpression' and 'code' are mandatory.";
        }

        if (!CronExpression.IsValidExpression(cronExpression))
        {
            return $"Error: The CRON expression '{cronExpression}' is syntactically invalid. Use 6-field Quartz syntax.";
        }

        try
        {
            var agentId = AgentToolFunction.CurrentAgentName;
            var scheduler = await schedulerFactory.GetScheduler(ct);

            var jobDetail = JobBuilder.Create<ExecuteJob>()
                .WithIdentity(new JobKey($"agent-cron-{Guid.NewGuid()}", agentId))
                .UsingJobData("AgentId", agentId)
                .UsingJobData("JobName", jobName)
                .UsingJobData("Code", code)
                .UsingJobData("Language", language.ToString())
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(new TriggerKey($"agent-trigger-{Guid.NewGuid()}", agentId))
                .WithCronSchedule(cronExpression)
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger, ct);

            return $"Success: Job '{jobName}' successfully saved. It will run autonomously on schedule: {cronExpression}.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error configuring agent cron trigger");
            return $"Error: Internal scheduler system initialization failed. Message: {ex.Message}";
        }
    }
}
