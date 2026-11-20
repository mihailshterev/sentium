using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.Impl.Matchers;
using Sentium.Infrastructure.Results;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Schedules: agent-created script jobs plus the agent and workflow Schedules users create here. Jobs are grouped by
/// their owning user, so a user only sees and changes their own; a Sovereign sees every user's (same bypass rule as the
/// data layer). Jobs the caller may not see return 404, so their existence is not disclosed.
/// </summary>
[ApiController]
[Authorize]
[Route($"{SchedulingRoutes.Prefix}/scheduler")]
internal sealed class SchedulerController(ISchedulerFactory schedulerFactory, ScheduleService schedules, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScheduledJobResponse>>> GetAllActiveJobsAsync(CancellationToken ct)
    {
        if (VisibleGroups() is not { } matcher)
        {
            return Ok(Array.Empty<ScheduledJobResponse>());
        }

        return Ok(await schedules.ListAsync(matcher, ct));
    }

    /// <summary>
    /// Creates an agent or workflow Schedule owned by the caller. The cron expression is read in the caller's time zone.
    /// </summary>
    [HttpPost("jobs")]
    [ProducesResponseType<ScheduledJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveScheduleRequest request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        return (await schedules.CreateAsync(userId, request, ct)).ToActionResult(this);
    }

    [HttpPut("jobs/{jobId}")]
    [ProducesResponseType<ScheduledJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(string jobId, [FromBody] SaveScheduleRequest request, CancellationToken ct)
    {
        var jobKey = await FindVisibleJobAsync(jobId, ct);
        return jobKey is null ? NotFound() : (await schedules.UpdateAsync(jobKey, request, ct)).ToActionResult(this);
    }

    [HttpPost("jobs/{jobId}/pause")]
    [ProducesResponseType<ScheduledJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PauseAsync(string jobId, CancellationToken ct)
    {
        var jobKey = await FindVisibleJobAsync(jobId, ct);
        return jobKey is null ? NotFound() : (await schedules.PauseAsync(jobKey, ct)).ToActionResult(this);
    }

    [HttpPost("jobs/{jobId}/resume")]
    [ProducesResponseType<ScheduledJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeAsync(string jobId, CancellationToken ct)
    {
        var jobKey = await FindVisibleJobAsync(jobId, ct);
        return jobKey is null ? NotFound() : (await schedules.ResumeAsync(jobKey, ct)).ToActionResult(this);
    }

    /// <summary>
    /// Cancels and permanently deletes a Schedule of any kind.
    /// </summary>
    [HttpDelete("jobs/{jobId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(string jobId, CancellationToken ct)
    {
        var jobKey = await FindVisibleJobAsync(jobId, ct);
        return jobKey is not null && await schedules.DeleteAsync(jobKey, ct) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Cancels and permanently deletes an agent-created script job.
    /// </summary>
    /// <param name="agentId">The agent that created the job (as returned by the list endpoint).</param>
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
        var jobKey = await FindVisibleJobAsync(jobId, ct);
        var detail = jobKey is null ? null : await scheduler.GetJobDetail(jobKey, ct);

        if (detail is null || !string.Equals(detail.JobDataMap.GetString(ScheduledJobData.AgentId), agentId, StringComparison.Ordinal))
        {
            return Problem(detail: $"No scheduled task '{jobId}' found for agent '{agentId}'.", statusCode: StatusCodes.Status404NotFound);
        }

        await scheduler.DeleteJob(detail.Key, ct);

        return NoContent();
    }

    [HttpGet("templates")]
    [ProducesResponseType<IReadOnlyList<ScheduleTemplate>>(StatusCodes.Status200OK)]
    public IActionResult GetTemplates() => Ok(ScheduleTemplates.All);

    /// <summary>
    /// The approval-gated tools an agent or workflow Schedule may pre-approve. Pass <paramref name="jobId"/> when editing,
    /// so the tools are those the Schedule's owner has.
    /// </summary>
    [HttpGet("pre-approvable-tools")]
    [ProducesResponseType<IReadOnlyList<ToolDescriptor>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreApprovableToolsAsync(
        [FromQuery] ScheduleKind kind,
        [FromQuery] string? agentName,
        [FromQuery] Guid? workflowId,
        [FromQuery] string? jobId,
        CancellationToken ct)
    {
        Guid? ownerId = currentUser.UserId;

        if (!string.IsNullOrWhiteSpace(jobId))
        {
            var jobKey = await FindVisibleJobAsync(jobId, ct);
            ownerId = jobKey is not null && Guid.TryParse(jobKey.Group, out var owner) ? owner : null;
        }

        if (ownerId is not { } id)
        {
            return NotFound();
        }

        return (await schedules.GetPreApprovableToolsAsync(id, kind, agentName, workflowId, ct)).ToActionResult(this);
    }

    private GroupMatcher<JobKey>? VisibleGroups()
    {
        if (currentUser.IsSovereign)
        {
            return GroupMatcher<JobKey>.AnyGroup();
        }

        return currentUser.UserId is { } userId
            ? GroupMatcher<JobKey>.GroupEquals(SchedulerDefaults.GroupFor(userId))
            : null;
    }

    private async Task<JobKey?> FindVisibleJobAsync(string jobId, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);

        if (!currentUser.IsSovereign)
        {
            if (currentUser.UserId is not { } userId)
            {
                return null;
            }

            var ownKey = new JobKey(jobId, SchedulerDefaults.GroupFor(userId));
            return await scheduler.CheckExists(ownKey, ct) ? ownKey : null;
        }

        var all = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), ct);
        return all.FirstOrDefault(k => string.Equals(k.Name, jobId, StringComparison.Ordinal));
    }
}
