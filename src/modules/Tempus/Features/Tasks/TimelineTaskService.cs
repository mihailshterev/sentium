using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Core.Text;
using Sentium.Infrastructure.Extensions;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;

namespace Sentium.Tempus.Features.Tasks;

public sealed class TimelineTaskService(TempusDbContext db, TimeProvider timeProvider) : ITimelineTaskService
{
    public async Task<TimelineTaskDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.TimelineTasks
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { Task = t, NoteCount = t.Notes.Count })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : TimelineMapping.ToDto(row.Task, row.NoteCount);
    }

    public async Task<Result<TimelineTaskDto>> CreateAsync(SaveTimelineTaskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Validate(request) is { } error)
        {
            return Result<TimelineTaskDto>.Invalid(error);
        }

        var now = timeProvider.GetUtcNow();
        var task = new TimelineTask
        {
            Id = Guid.CreateVersion7(),
            Origin = TimelineAuthorKind.User,
            CreatedAt = now
        };

        Apply(task, request, now);
        db.TimelineTasks.Add(task);
        await db.SaveChangesAsync(ct);

        return Result<TimelineTaskDto>.Success(TimelineMapping.ToDto(task, 0));
    }

    public async Task<Result<TimelineTaskDto>> UpdateAsync(Guid id, SaveTimelineTaskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Validate(request) is { } error)
        {
            return Result<TimelineTaskDto>.Invalid(error);
        }

        var found = await db.Database.ExecuteInTransactionAsync(() => SaveTaskChangesAsync(id, request, ct), ct: ct);

        return found ? Result<TimelineTaskDto>.Success((await GetAsync(id, ct))!) : Result<TimelineTaskDto>.NotFound();
    }

    public async Task<Result<TimelineTaskDto>> ScheduleAsync(Guid id, ScheduleTimelineTaskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TaskRules.ValidatePlacement(request.Year, request.Month, request.Date) is { } error)
        {
            return Result<TimelineTaskDto>.Invalid(error);
        }

        var found = await db.Database.ExecuteInTransactionAsync(() => MoveTaskAsync(id, request, ct), ct: ct);

        return found ? Result<TimelineTaskDto>.Success((await GetAsync(id, ct))!) : Result<TimelineTaskDto>.NotFound();
    }

    public async Task<Result<TimelineTaskDto>> SetStatusAsync(Guid id, TimelineTaskStatus status, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(status))
        {
            return Result<TimelineTaskDto>.Invalid("Unknown task status.");
        }

        var found = await db.Database.ExecuteInTransactionAsync(() => ChangeTaskStatusAsync(id, status, ct), ct: ct);

        return found ? Result<TimelineTaskDto>.Success((await GetAsync(id, ct))!) : Result<TimelineTaskDto>.NotFound();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => await db.TimelineTasks.Where(t => t.Id == id).ExecuteDeleteAsync(ct) > 0;

    private async Task<bool> SaveTaskChangesAsync(Guid id, SaveTimelineTaskRequest request, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var task = await db.TimelineTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var moved = !TaskRules.IsAt(task, request.Year, request.Month, request.Date);
        var completed = task.Status != TimelineTaskStatus.Done && request.Status == TimelineTaskStatus.Done;

        Apply(task, request, now);
        await db.SaveChangesAsync(ct);

        if (moved)
        {
            await TaskRules.AfterMoveAsync(db, task, now, keepProposalId: null, ct);
        }
        else if (completed)
        {
            await TaskRules.SupersedePendingReschedulesAsync(db, task.Id, now, keepProposalId: null, ct);
        }

        return true;
    }

    private async Task<bool> MoveTaskAsync(Guid id, ScheduleTimelineTaskRequest request, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var task = await db.TimelineTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return false;
        }

        if (TaskRules.IsAt(task, request.Year, request.Month, request.Date))
        {
            return true;
        }

        var now = timeProvider.GetUtcNow();
        task.Year = request.Year;
        task.Month = request.Month;
        task.Date = request.Date;
        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await TaskRules.AfterMoveAsync(db, task, now, keepProposalId: null, ct);
        return true;
    }

    private async Task<bool> ChangeTaskStatusAsync(Guid id, TimelineTaskStatus status, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var task = await db.TimelineTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return false;
        }

        if (task.Status == status)
        {
            return true;
        }

        var now = timeProvider.GetUtcNow();
        SetStatus(task, status, now);
        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        if (status == TimelineTaskStatus.Done)
        {
            await TaskRules.SupersedePendingReschedulesAsync(db, task.Id, now, keepProposalId: null, ct);
        }

        return true;
    }

    private static string? Validate(SaveTimelineTaskRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Title)
            ? "A task needs a title."
            : TaskRules.ValidatePlacement(request.Year, request.Month, request.Date) ?? TaskRules.ValidateDueDate(request.DueDate);
    }

    private static void Apply(TimelineTask task, SaveTimelineTaskRequest request, DateTimeOffset now)
    {
        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Year = request.Year;
        task.Month = request.Month;
        task.Date = request.Date;
        task.DueDate = request.DueDate;
        task.Priority = request.Priority;
        task.Tags = TagNormalizer.Normalize(request.Tags);
        task.AssignedAgent = string.IsNullOrWhiteSpace(request.AssignedAgent) ? null : request.AssignedAgent.Trim();
        task.UpdatedAt = now;
        SetStatus(task, request.Status, now);
    }

    internal static void SetStatus(TimelineTask task, TimelineTaskStatus status, DateTimeOffset now)
    {
        task.CompletedAt = status == TimelineTaskStatus.Done ? task.CompletedAt ?? now : null;
        task.Status = status;
    }
}
