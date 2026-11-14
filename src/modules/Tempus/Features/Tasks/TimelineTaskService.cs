using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Core.Text;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features.Calendar;
using Sentium.Tempus.Features.Series;

namespace Sentium.Tempus.Features.Tasks;

internal sealed partial class TimelineTaskService(
    TempusDbContext db,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    IUserCalendarProvider calendars,
    TimelineSeriesWriter seriesWriter) : ITimelineTaskService
{
    private Guid UserId => currentUser.UserId ?? Guid.Empty;

    public async Task<TimelineTaskDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await TimelineTaskRow.Select(db.TimelineTasks.AsNoTracking().Where(t => t.Id == id)).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var calendar = await calendars.GetAsync(UserId, ct);
        return TimelineMapping.ToDto(row.Task, row.NoteCount, row.Series, calendar.Today(timeProvider.GetUtcNow()));
    }

    public async Task<Result<TimelineTaskDto>> CreateAsync(SaveTimelineTaskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Validate(request) is { } error)
        {
            return Result<TimelineTaskDto>.Invalid(error);
        }

        var calendar = await calendars.GetAsync(UserId, ct);
        var id = request.Repeat is { } repeat
            ? await db.Database.ExecuteInTransactionAsync(() => CreateSeriesAsync(request, repeat, calendar, ct), ct: ct)
            : await CreateOneAsync(request, calendar, ct);

        return Result<TimelineTaskDto>.Success((await GetAsync(id, ct))!);
    }

    public async Task<Result<TimelineTaskDto>> UpdateAsync(Guid id, SaveTimelineTaskRequest request, TimelineEditScope scope = TimelineEditScope.One, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kind = await db.TimelineTasks.Where(t => t.Id == id).Select(t => (TimelineEntryKind?)t.Kind).FirstOrDefaultAsync(ct);
        if (kind is not { } existingKind)
        {
            return Result<TimelineTaskDto>.NotFound();
        }

        var typed = request with { Kind = existingKind };
        if (Validate(typed) is { } error)
        {
            return Result<TimelineTaskDto>.Invalid(error);
        }

        var calendar = await calendars.GetAsync(UserId, ct);
        var savedId = await db.Database.ExecuteInTransactionAsync(() => SaveTaskChangesAsync(id, typed, scope, calendar, ct), ct: ct);

        return savedId is { } found ? Result<TimelineTaskDto>.Success((await GetAsync(found, ct))!) : Result<TimelineTaskDto>.NotFound();
    }

    public async Task<Result<TimelineTaskDto>> ScheduleAsync(Guid id, ScheduleTimelineTaskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TaskRules.ValidatePlacement(request.Year, request.Month, request.Date) is { } error)
        {
            return Result<TimelineTaskDto>.Invalid(error);
        }

        var calendar = await calendars.GetAsync(UserId, ct);
        var found = await db.Database.ExecuteInTransactionAsync(() => MoveTaskAsync(id, request, calendar, ct), ct: ct);

        return found ? Result<TimelineTaskDto>.Success((await GetAsync(id, ct))!) : Result<TimelineTaskDto>.NotFound();
    }

    public async Task<Result<TimelineTaskDto>> SetStatusAsync(Guid id, TimelineTaskStatus status, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(status))
        {
            return Result<TimelineTaskDto>.Invalid("Unknown task status.");
        }

        var result = await db.Database.ExecuteInTransactionAsync(() => ChangeTaskStatusAsync(id, status, ct), ct: ct);

        return result.IsSuccess ? Result<TimelineTaskDto>.Success((await GetAsync(id, ct))!) : result.Propagate<bool, TimelineTaskDto>();
    }

    public async Task<bool> DeleteAsync(Guid id, TimelineEditScope scope = TimelineEditScope.One, CancellationToken ct = default)
    {
        var calendar = await calendars.GetAsync(UserId, ct);
        return await db.Database.ExecuteInTransactionAsync(() => DeleteEntryAsync(id, scope, calendar, ct), ct: ct);
    }

    private async Task<Guid> CreateOneAsync(SaveTimelineTaskRequest request, UserCalendar calendar, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var task = new TimelineTask
        {
            Id = Guid.CreateVersion7(),
            Kind = request.Kind,
            Origin = TimelineAuthorKind.User,
            CreatedAt = now
        };

        Apply(task, request, now);
        TaskRules.ArmReminder(task, calendar);
        db.TimelineTasks.Add(task);
        await db.SaveChangesAsync(ct);

        return task.Id;
    }

    private async Task<Guid?> SaveTaskChangesAsync(Guid id, SaveTimelineTaskRequest request, TimelineEditScope scope, UserCalendar calendar, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var task = await db.TimelineTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var moved = !TaskRules.IsAt(task, request.Year, request.Month, request.Date);
        var completed = task.Status != TimelineTaskStatus.Done && request.Status == TimelineTaskStatus.Done;
        var rearm = moved || task.StartTime != request.StartTime || task.ReminderOffsetMinutes != request.ReminderOffsetMinutes;

        Apply(task, request, now);
        if (rearm)
        {
            TaskRules.ArmReminder(task, calendar);
        }

        await ApplySeriesEditAsync(task, request, scope, calendar, now, ct);
        await db.SaveChangesAsync(ct);

        if (moved)
        {
            await TaskRules.AfterMoveAsync(db, task, now, keepProposalId: null, ct);
        }
        else if (completed)
        {
            await TaskRules.SupersedePendingReschedulesAsync(db, task.Id, now, keepProposalId: null, ct);
        }

        return task.Id;
    }

    private async Task<bool> MoveTaskAsync(Guid id, ScheduleTimelineTaskRequest request, UserCalendar calendar, CancellationToken ct)
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
        TaskRules.MoveTo(task, request.Year, request.Month, request.Date, calendar, now);
        await db.SaveChangesAsync(ct);

        await TaskRules.AfterMoveAsync(db, task, now, keepProposalId: null, ct);
        return true;
    }

    private async Task<Result<bool>> ChangeTaskStatusAsync(Guid id, TimelineTaskStatus status, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var task = await db.TimelineTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return Result<bool>.NotFound();
        }

        if (task.Kind != TimelineEntryKind.Task)
        {
            return Result<bool>.Invalid("A Calendar Event has no status; it is over once its day has passed.");
        }

        if (task.Status == status)
        {
            return Result<bool>.Success(true);
        }

        var now = timeProvider.GetUtcNow();
        SetStatus(task, status, now);
        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        if (status == TimelineTaskStatus.Done)
        {
            await TaskRules.SupersedePendingReschedulesAsync(db, task.Id, now, keepProposalId: null, ct);
        }

        return Result<bool>.Success(true);
    }

    private static string? Validate(SaveTimelineTaskRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Title)
            ? "A task needs a title."
            : TaskRules.ValidatePlacement(request.Year, request.Month, request.Date)
                ?? TaskRules.ValidateDueDate(request.DueDate)
                ?? TaskRules.ValidateEntry(request);
    }

    /// <summary>
    /// Copies the request onto the Entry. A Calendar Event keeps the neutral Task fields (normal priority, to do),
    /// a Task never has an end day.
    /// </summary>
    private static void Apply(TimelineTask task, SaveTimelineTaskRequest request, DateTimeOffset now)
    {
        var isTask = task.Kind == TimelineEntryKind.Task;

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Year = request.Year;
        task.Month = request.Month;
        task.Date = request.Date;
        task.StartTime = request.Date is null ? null : request.StartTime;
        task.EndDate = isTask || request.Date is null ? null : request.EndDate;
        task.DueDate = isTask ? request.DueDate : null;
        task.Priority = isTask ? request.Priority ?? TimelineTaskPriority.Normal : TimelineTaskPriority.Normal;
        task.Tags = TagNormalizer.Normalize(request.Tags);
        task.AssignedAgent = isTask && !string.IsNullOrWhiteSpace(request.AssignedAgent) ? request.AssignedAgent.Trim() : null;
        task.ReminderOffsetMinutes = request.ReminderOffsetMinutes;
        task.UpdatedAt = now;
        SetStatus(task, isTask ? request.Status ?? task.Status : TimelineTaskStatus.Todo, now);
    }

    internal static void SetStatus(TimelineTask task, TimelineTaskStatus status, DateTimeOffset now)
    {
        task.CompletedAt = status == TimelineTaskStatus.Done ? task.CompletedAt ?? now : null;
        task.Status = status;
    }
}
