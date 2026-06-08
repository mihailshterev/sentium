using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.Impl.Matchers;
using Sentium.AgentRuntime.Core.Dtos;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Provides endpoints for managing and monitoring scheduled background cron jobs for agents across the platform.
/// </summary>
[ApiController]
[Authorize]
[Route("scheduler")]
public sealed class SchedulerController(ISchedulerFactory schedulerFactory) : ControllerBase
{
    /// <summary>
    /// Retrieves all active automated scheduled cron jobs running across the platform,
    /// grouped cleanly by the AgentId that owns them.
    /// </summary>
    [HttpGet("jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllActiveJobsAsync(CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var activeJobsList = new List<ScheduledJobResponse>();

        var jobGroups = await scheduler.GetJobGroupNames(ct);

        foreach (var groupName in jobGroups)
        {
            var groupMatcher = GroupMatcher<JobKey>.GroupEquals(groupName);
            var jobKeys = await scheduler.GetJobKeys(groupMatcher, ct);

            foreach (var jobKey in jobKeys)
            {
                var detail = await scheduler.GetJobDetail(jobKey, ct);
                var triggers = await scheduler.GetTriggersOfJob(jobKey, ct);
                var trigger = triggers.FirstOrDefault();

                if (detail is null)
                {
                    continue;
                }

                var cronTrigger = trigger as ICronTrigger;
                var nextFireTime = trigger?.GetNextFireTimeUtc();
                var previousFireTime = trigger?.GetPreviousFireTimeUtc();

                activeJobsList.Add(new ScheduledJobResponse(
                    JobId: jobKey.Name,
                    AgentId: jobKey.Group,
                    JobName: detail.JobDataMap.GetString("JobName") ?? "UnnamedTask",
                    Language: detail.JobDataMap.GetString("Language") ?? "Python",
                    CronExpression: cronTrigger?.CronExpressionString ?? "Unknown",
                    PreviousRun: previousFireTime?.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    NextRun: nextFireTime?.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    Status: trigger is null ? "Orphaned" : (await scheduler.GetTriggerState(trigger.Key, ct)).ToString()
                ));
            }
        }

        return Ok(activeJobsList);
    }

    /// <summary>
    /// Cancels and deletes an active background cron job permanently out of the scheduler engine.
    /// </summary>
    /// <param name="agentId">The group name partition key (AgentId).</param>
    /// <param name="jobId">The unique random tracking identifier (JobId).</param>
    [HttpDelete("agents/{agentId}/jobs/{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteScheduledJobAsync(string agentId, string jobId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(jobId))
        {
            return Problem(detail: "Identifiers cannot be blank tokens.", statusCode: StatusCodes.Status400BadRequest);
        }

        var scheduler = await schedulerFactory.GetScheduler(ct);

        var targetJobKey = new JobKey(jobId, agentId);

        if (!await scheduler.CheckExists(targetJobKey, ct))
        {
            return Problem(detail: $"No scheduled task found matching Key: {jobId} under Agent Partition: {agentId}", statusCode: StatusCodes.Status404NotFound);
        }

        await scheduler.DeleteJob(targetJobKey, ct);

        return NoContent();
    }
}
