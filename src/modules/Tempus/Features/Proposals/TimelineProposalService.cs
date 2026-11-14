using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features.Calendar;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Proposals;

internal sealed class TimelineProposalService(
    TempusDbContext db,
    ITimelineTaskService taskService,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    IUserCalendarProvider calendars) : ITimelineProposalService
{
    private const int MaxListed = 200;

    public async Task<IReadOnlyList<TimelineProposalDto>> ListAsync(TimelineProposalStatus? status, CancellationToken ct = default)
    {
        var wanted = status ?? TimelineProposalStatus.Pending;

        var proposals = await db.TimelineProposals
            .AsNoTracking()
            .Where(p => p.Status == wanted)
            .OrderByDescending(p => p.CreatedAt)
            .Take(MaxListed)
            .ToListAsync(ct);

        return [.. proposals.Select(TimelineMapping.ToDto)];
    }

    public async Task<Result<TimelineTaskDto>> AcceptAsync(Guid id, CancellationToken ct = default)
    {
        var calendar = await calendars.GetAsync(currentUser.UserId ?? Guid.Empty, ct);
        var result = await db.Database.ExecuteInTransactionAsync(() => AcceptPendingAsync(id, calendar, ct), ct: ct);

        return result.IsSuccess
            ? Result<TimelineTaskDto>.Success((await taskService.GetAsync(result.Value, ct))!)
            : result.Propagate<Guid, TimelineTaskDto>();
    }

    public async Task<Result<TimelineProposalDto>> RejectAsync(Guid id, CancellationToken ct = default)
    {
        if (!await TryResolveAsync(id, TimelineProposalStatus.Rejected, timeProvider.GetUtcNow(), ct))
        {
            var existing = await db.TimelineProposals.AsNoTracking().Where(p => p.Id == id).Select(p => (TimelineProposalStatus?)p.Status).FirstOrDefaultAsync(ct);
            return existing is { } status
                ? Result<TimelineProposalDto>.Conflict(AlreadyResolved(status))
                : Result<TimelineProposalDto>.NotFound();
        }

        var proposal = await db.TimelineProposals.AsNoTracking().FirstAsync(p => p.Id == id, ct);
        return Result<TimelineProposalDto>.Success(TimelineMapping.ToDto(proposal));
    }

    private async Task<Result<Guid>> AcceptPendingAsync(Guid id, UserCalendar calendar, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var proposal = await db.TimelineProposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proposal is null)
        {
            return Result<Guid>.NotFound();
        }

        if (proposal.Status != TimelineProposalStatus.Pending)
        {
            return Result<Guid>.Conflict(AlreadyResolved(proposal.Status));
        }

        var now = timeProvider.GetUtcNow();

        return proposal.Kind == TimelineProposalKind.Reschedule
            ? await AcceptRescheduleAsync(proposal, calendar, now, ct)
            : await AcceptCreateAsync(proposal, now, ct);
    }

    private async Task<Result<Guid>> AcceptCreateAsync(TimelineProposal proposal, DateTimeOffset now, CancellationToken ct)
    {
        if (!await TryResolveAsync(proposal.Id, TimelineProposalStatus.Accepted, now, ct))
        {
            return Result<Guid>.Conflict("The proposal was resolved meanwhile.");
        }

        var task = new TimelineTask
        {
            Id = Guid.CreateVersion7(),
            UserId = proposal.UserId,
            Kind = proposal.Kind == TimelineProposalKind.CreateEvent ? TimelineEntryKind.CalendarEvent : TimelineEntryKind.Task,
            StartTime = proposal.StartTime,
            EndDate = proposal.EndDate,
            Title = proposal.Title,
            Description = proposal.Description,
            Year = proposal.Year,
            Month = proposal.Month,
            Date = proposal.Date,
            DueDate = proposal.DueDate,
            Priority = proposal.Priority,
            Status = TimelineTaskStatus.Todo,
            Tags = [.. proposal.Tags],
            AssignedAgent = proposal.AssignedAgent,
            Origin = TimelineAuthorKind.Agent,
            CreatedByAgent = proposal.ProposedBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.TimelineTasks.Add(task);
        await db.SaveChangesAsync(ct);

        return Result<Guid>.Success(task.Id);
    }

    private async Task<Result<Guid>> AcceptRescheduleAsync(TimelineProposal proposal, UserCalendar calendar, DateTimeOffset now, CancellationToken ct)
    {
        var task = proposal.TaskId is { } taskId
            ? await db.TimelineTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            : null;

        var stale = task is null
            || (task.Kind == TimelineEntryKind.Task && task.Status == TimelineTaskStatus.Done)
            || !TaskRules.IsAt(task, proposal.FromYear ?? 0, proposal.FromMonth ?? 0, proposal.FromDate);

        if (stale)
        {
            // Committed with the conflict, so the stale suggestion stops showing.
            await TryResolveAsync(proposal.Id, TimelineProposalStatus.Superseded, now, ct);
            return Result<Guid>.Conflict("The entry has changed since this move was proposed.");
        }

        if (!await TryResolveAsync(proposal.Id, TimelineProposalStatus.Accepted, now, ct))
        {
            return Result<Guid>.Conflict("The proposal was resolved meanwhile.");
        }

        TaskRules.MoveTo(task!, proposal.Year, proposal.Month, proposal.Date, calendar, now);
        await db.SaveChangesAsync(ct);

        await TaskRules.AfterMoveAsync(db, task!, now, keepProposalId: proposal.Id, ct);

        return Result<Guid>.Success(task!.Id);
    }

    /// <summary>
    /// Pending → <paramref name="status"/> as one guarded update, so two concurrent answers cannot both win.
    /// </summary>
    private async Task<bool> TryResolveAsync(Guid id, TimelineProposalStatus status, DateTimeOffset now, CancellationToken ct)
    {
        return await db.TimelineProposals
            .Where(p => p.Id == id && p.Status == TimelineProposalStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.ResolvedAt, now), ct) == 1;
    }

    private static string AlreadyResolved(TimelineProposalStatus status)
        => $"This proposal is no longer pending ({status.ToString().ToLowerInvariant()}).";
}
