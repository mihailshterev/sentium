using System.Globalization;
using Quartz;
using Quartz.Impl.Matchers;
using Sentium.Core.Results;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// Creates, edits, pauses and resumes Schedules. Callers resolve which job the user may address (see
/// <see cref="SchedulerController"/>); everything here acts for the job's owner, the job group.
/// </summary>
internal sealed class ScheduleService(
    ISchedulerFactory schedulerFactory,
    IUnattendedRunLauncher launcher,
    OwnerTimeZoneResolver timeZones,
    TimeProvider clock,
    ILogger<ScheduleService> logger)
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss 'UTC'";
    private const string JobPrefix = "schedule-";
    private const string TriggerPrefix = "schedule-trigger-";

    public async Task<IReadOnlyList<ScheduledJobResponse>> ListAsync(GroupMatcher<JobKey> groups, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var jobKeys = await scheduler.GetJobKeys(groups, ct);
        var jobs = new List<ScheduledJobResponse>(jobKeys.Count);

        foreach (var jobKey in jobKeys)
        {
            if (await scheduler.GetJobDetail(jobKey, ct) is { } detail)
            {
                jobs.Add(await ToResponseAsync(scheduler, detail, ct));
            }
        }

        return jobs;
    }

    public async Task<Result<ScheduledJobResponse>> CreateAsync(Guid ownerId, SaveScheduleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind == ScheduleKind.Script)
        {
            return Result<ScheduledJobResponse>.Invalid("Script schedules are created by agents; choose an agent or a workflow.");
        }

        var zone = await timeZones.ResolveAsync(ownerId, ct);
        if (ScheduleCron.Validate(request.CronExpression, zone, clock.GetUtcNow()) is { } cronError)
        {
            return Result<ScheduledJobResponse>.Invalid(cronError);
        }

        var definition = await BuildDefinitionAsync(ownerId, request, ct);
        if (!definition.IsSuccess)
        {
            return Result<ScheduledJobResponse>.Invalid(definition.Error!);
        }

        var scheduler = await schedulerFactory.GetScheduler(ct);
        var group = SchedulerDefaults.GroupFor(ownerId);

        var owned = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group), ct);
        if (owned.Count >= SchedulerDefaults.MaxJobsPerUser)
        {
            return Result<ScheduledJobResponse>.Conflict($"You already have {owned.Count} schedules (limit {SchedulerDefaults.MaxJobsPerUser}). Delete one first.");
        }

        var job = JobBuilder.Create<AgentRunJob>()
            .WithIdentity(new JobKey($"{JobPrefix}{Guid.NewGuid()}", group))
            .WithDescription(definition.Value!.Name)
            .Build();
        definition.Value.WriteTo(job.JobDataMap);

        await scheduler.ScheduleJob(job, BuildTrigger(job.Key, request.CronExpression, zone), ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} created {Kind} schedule {JobKey} with cron '{Cron}' ({Zone}).", ownerId, request.Kind, job.Key, request.CronExpression, zone.Id);
        }

        return Result<ScheduledJobResponse>.Success(await ToResponseAsync(scheduler, job, ct));
    }

    public async Task<Result<ScheduledJobResponse>> UpdateAsync(JobKey jobKey, SaveScheduleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobKey);
        ArgumentNullException.ThrowIfNull(request);

        var scheduler = await schedulerFactory.GetScheduler(ct);
        if (await scheduler.GetJobDetail(jobKey, ct) is not { } detail || !Guid.TryParse(jobKey.Group, out var ownerId))
        {
            return Result<ScheduledJobResponse>.NotFound();
        }

        var kind = ScheduleDefinition.ReadKind(detail.JobDataMap);
        if ((kind == ScheduleKind.Script) != (request.Kind == ScheduleKind.Script))
        {
            return Result<ScheduledJobResponse>.Invalid("A script schedule cannot become an agent or workflow schedule, or the other way round.");
        }

        // Script schedules run in UTC; the others follow the owner's current zone.
        var zone = kind == ScheduleKind.Script ? TimeZoneInfo.Utc : await timeZones.ResolveAsync(ownerId, ct);
        if (ScheduleCron.Validate(request.CronExpression, zone, clock.GetUtcNow()) is { } cronError)
        {
            return Result<ScheduledJobResponse>.Invalid(cronError);
        }

        var updated = detail.GetJobBuilder().WithDescription(request.Name.Trim()).Build();

        if (kind == ScheduleKind.Script)
        {
            updated.JobDataMap[ScheduledJobData.JobName] = request.Name.Trim();
        }
        else
        {
            var next = await BuildDefinitionAsync(ownerId, request, ct);
            if (!next.IsSuccess)
            {
                return Result<ScheduledJobResponse>.Invalid(next.Error!);
            }

            var definition = request.PreApprovedTools is null && ScheduleDefinition.Read(detail.JobDataMap) is { } previous
                ? next.Value!.CarryPreApprovalsFrom(previous)
                : next.Value!;
            definition.WriteTo(updated.JobDataMap);
        }

        await scheduler.AddJob(updated, replace: true, storeNonDurableWhileAwaitingScheduling: true, ct);
        await ReplaceTriggerAsync(scheduler, jobKey, request.CronExpression, zone, ct);

        return Result<ScheduledJobResponse>.Success(await ToResponseAsync(scheduler, updated, ct));
    }

    public async Task<Result<ScheduledJobResponse>> PauseAsync(JobKey jobKey, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        if (await scheduler.GetJobDetail(jobKey, ct) is not { } detail)
        {
            return Result<ScheduledJobResponse>.NotFound();
        }

        await scheduler.PauseJob(jobKey, ct);
        return Result<ScheduledJobResponse>.Success(await ToResponseAsync(scheduler, detail, ct));
    }

    /// <summary>
    /// Resumes with a fresh trigger: resuming the old one would treat every fire missed while paused as a misfire and run
    /// once immediately.
    /// </summary>
    public async Task<Result<ScheduledJobResponse>> ResumeAsync(JobKey jobKey, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        if (await scheduler.GetJobDetail(jobKey, ct) is not { } detail)
        {
            return Result<ScheduledJobResponse>.NotFound();
        }

        if ((await scheduler.GetTriggersOfJob(jobKey, ct)).FirstOrDefault() is ICronTrigger current)
        {
            await scheduler.RescheduleJob(current.Key, BuildTrigger(jobKey, current.CronExpressionString!, current.TimeZone), ct);
        }
        else
        {
            await scheduler.ResumeJob(jobKey, ct);
        }

        return Result<ScheduledJobResponse>.Success(await ToResponseAsync(scheduler, detail, ct));
    }

    public async Task<bool> DeleteAsync(JobKey jobKey, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        return await scheduler.DeleteJob(jobKey, ct);
    }

    public async Task<Result<IReadOnlyList<ToolDescriptor>>> GetPreApprovableToolsAsync(
        Guid ownerId,
        ScheduleKind kind,
        string? agentName,
        Guid? workflowId,
        CancellationToken ct)
    {
        if (kind == ScheduleKind.Script)
        {
            return Result<IReadOnlyList<ToolDescriptor>>.Invalid("Script schedules have no pre-approvals.");
        }

        var target = await launcher.ResolveTargetAsync(ToRunKind(kind), agentName, workflowId, ownerId, ct);
        return target is null
            ? Result<IReadOnlyList<ToolDescriptor>>.NotFound()
            : Result<IReadOnlyList<ToolDescriptor>>.Success(target.GatedTools);
    }

    /// <summary>
    /// The Schedule the request describes, with the target's canonical name. Fails when the owner has no such agent or
    /// workflow, or when a requested Pre-approval is not an approval-gated tool of it.
    /// </summary>
    private async Task<Result<ScheduleDefinition>> BuildDefinitionAsync(Guid ownerId, SaveScheduleRequest request, CancellationToken ct)
    {
        var target = await launcher.ResolveTargetAsync(ToRunKind(request.Kind), request.AgentName, request.WorkflowId, ownerId, ct);
        if (target is null)
        {
            return Result<ScheduleDefinition>.Invalid(request.Kind == ScheduleKind.Agent
                ? $"You have no agent named '{request.AgentName}'."
                : "You have no such workflow.");
        }

        var gated = target.GatedTools.ToDictionary(t => t.Name, t => t.Name, StringComparer.OrdinalIgnoreCase);
        var preApproved = new List<string>();

        foreach (var tool in (request.PreApprovedTools ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!gated.TryGetValue(tool, out var canonical))
            {
                return Result<ScheduleDefinition>.Invalid($"'{tool}' is not an approval-gated tool of {target.Name}, so it cannot be pre-approved.");
            }

            preApproved.Add(canonical);
        }

        return Result<ScheduleDefinition>.Success(new ScheduleDefinition(
            request.Kind,
            request.Name.Trim(),
            request.Kind == ScheduleKind.Agent ? target.Name : null,
            request.Kind == ScheduleKind.Workflow ? request.WorkflowId : null,
            target.Name,
            request.Prompt?.Trim() ?? string.Empty,
            preApproved));
    }

    /// <summary>
    /// Keeps a paused Schedule paused: the new trigger would otherwise start in the normal state.
    /// </summary>
    private static async Task ReplaceTriggerAsync(IScheduler scheduler, JobKey jobKey, string cronExpression, TimeZoneInfo zone, CancellationToken ct)
    {
        var trigger = BuildTrigger(jobKey, cronExpression, zone);

        if ((await scheduler.GetTriggersOfJob(jobKey, ct)).FirstOrDefault() is not { } current)
        {
            await scheduler.ScheduleJob(trigger, ct);
            return;
        }

        var paused = await scheduler.GetTriggerState(current.Key, ct) == TriggerState.Paused;
        await scheduler.RescheduleJob(current.Key, trigger, ct);

        if (paused)
        {
            await scheduler.PauseTrigger(trigger.Key, ct);
        }
    }

    private static ITrigger BuildTrigger(JobKey jobKey, string cronExpression, TimeZoneInfo zone)
    {
        return TriggerBuilder.Create()
            .WithIdentity(new TriggerKey($"{TriggerPrefix}{Guid.NewGuid()}", jobKey.Group))
            .ForJob(jobKey)
            .WithCronSchedule(cronExpression, schedule => schedule
                .InTimeZone(zone)
                .WithMisfireHandlingInstructionFireAndProceed())
            .Build();
    }

    private static UnattendedRunKind ToRunKind(ScheduleKind kind) =>
        kind == ScheduleKind.Workflow ? UnattendedRunKind.Workflow : UnattendedRunKind.Agent;

    private static async Task<ScheduledJobResponse> ToResponseAsync(IScheduler scheduler, IJobDetail detail, CancellationToken ct)
    {
        var data = detail.JobDataMap;
        var trigger = (await scheduler.GetTriggersOfJob(detail.Key, ct)).FirstOrDefault();
        var state = trigger is null ? (TriggerState?)null : await scheduler.GetTriggerState(trigger.Key, ct);
        var definition = ScheduleDefinition.Read(data);
        var lastRun = ScheduleLastRun.Read(data);

        return new ScheduledJobResponse(
            JobId: detail.Key.Name,
            AgentId: data.Find(ScheduledJobData.AgentId) ?? "Agent",
            JobName: data.Find(ScheduledJobData.JobName) ?? "UnnamedTask",
            Language: data.Find(ScheduledJobData.Language) ?? (definition is null ? nameof(ScheduledJobLanguage.Python) : string.Empty),
            CronExpression: (trigger as ICronTrigger)?.CronExpressionString ?? "Unknown",
            PreviousRun: FormatTimestamp(trigger?.GetPreviousFireTimeUtc()),
            NextRun: state == TriggerState.Paused ? null : FormatTimestamp(trigger?.GetNextFireTimeUtc()),
            Status: state?.ToString() ?? "Orphaned",
            Kind: definition?.Kind ?? ScheduleKind.Script,
            WorkflowId: definition?.WorkflowId,
            TargetName: definition?.TargetName ?? string.Empty,
            Prompt: definition?.Prompt ?? string.Empty,
            PreApprovedTools: definition?.PreApprovedTools ?? [],
            TimeZone: (trigger as ICronTrigger)?.TimeZone.Id ?? TimeZoneInfo.Utc.Id,
            Paused: state == TriggerState.Paused,
            LastRun: lastRun is null ? null : new ScheduleLastRunResponse(lastRun.RunId, lastRun.Status, lastRun.At));
    }

    private static string? FormatTimestamp(DateTimeOffset? value) =>
        value?.ToString(TimestampFormat, CultureInfo.InvariantCulture);
}
