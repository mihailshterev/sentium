using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Core.Text;
using Sentium.Infrastructure.Extensions;
using Sentium.Nuntius.Contracts;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features.Calendar;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Agents;

/// <summary>
/// Scoped implementation behind <see cref="ITimelineAgentAccess"/>. It runs in a system scope (see
/// <see cref="ScopedTimelineAgentAccess"/>), which bypasses the DbContext's per-user filter - so EVERY query here
/// filters on the explicit <c>userId</c>, and every insert sets it.
/// </summary>
internal sealed class TimelineAgentAccessService(
    TempusDbContext db,
    TimeProvider timeProvider,
    INotifier notifier,
    IUserCalendarProvider calendars) : ITimelineAgentAccess
{
    /// <summary>
    /// One unread suggestions Notification per user: an agent proposing many tasks in one run updates it instead of
    /// flooding the Inbox.
    /// </summary>
    private const string ProposalDedupeKey = NotificationCategories.TimelineProposal;

    public async Task<TimelineAgentOverview> GetOverviewAsync(Guid userId, int? year = null, int upcomingDays = 14, CancellationToken ct = default)
    {
        var today = await TodayAsync(userId, ct);
        var overviewYear = year is { } y && TempusLimits.IsValidYear(y) ? y : today.Year;
        var days = Math.Clamp(upcomingDays, 1, TempusLimits.MaxUpcomingDays);
        var lastDay = today.AddDays(days - 1);

        var entries = await Tasks(userId)
            .Where(t => t.Year == overviewYear)
            .Select(t => new { t.Kind, t.Month, t.Date, t.DueDate, t.Status })
            .ToListAsync(ct);

        var tasks = entries.Where(t => t.Kind == TimelineEntryKind.Task).ToList();

        var pendingByMonth = await Proposals(userId)
            .Where(p => p.Status == TimelineProposalStatus.Pending && p.Year == overviewYear)
            .GroupBy(p => p.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Month, g => g.Count, ct);

        var upcoming = await Tasks(userId)
            .Where(t => t.Kind == TimelineEntryKind.Task && t.Status != TimelineTaskStatus.Done && t.Date != null && t.Date >= today && t.Date <= lastDay)
            .GroupBy(t => t.Date!.Value)
            .Select(g => new { Date = g.Key, Open = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Open, ct);

        var upcomingEvents = await Tasks(userId)
            .Where(t => t.Kind == TimelineEntryKind.CalendarEvent && t.Date != null && t.Date >= today && t.Date <= lastDay)
            .GroupBy(t => t.Date!.Value)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Count, ct);

        var pinnedOpenThisMonth = await Tasks(userId)
            .CountAsync(t => t.Kind == TimelineEntryKind.Task && t.Year == today.Year && t.Month == today.Month && t.Date == null && t.Status != TimelineTaskStatus.Done, ct);

        var pendingTotal = await Proposals(userId).CountAsync(p => p.Status == TimelineProposalStatus.Pending, ct);

        var months = Enumerable.Range(1, 12).Select(month =>
        {
            var inMonth = tasks.Where(t => t.Month == month).ToList();
            return new TimelineMonthLoad(
                month,
                inMonth.Count(t => t.Status != TimelineTaskStatus.Done),
                inMonth.Count(t => t.Status == TimelineTaskStatus.Done),
                inMonth.Count(t => TimelineCalendar.IsOverdue(t.Status, t.DueDate, t.Date, overviewYear, month, today)),
                pendingByMonth.GetValueOrDefault(month),
                entries.Count(e => e.Kind == TimelineEntryKind.CalendarEvent && e.Month == month));
        }).ToList();

        var upcomingDaysLoad = Enumerable.Range(0, days)
            .Select(offset => today.AddDays(offset))
            .Select(day => new TimelineDayLoad(day, upcoming.GetValueOrDefault(day), upcomingEvents.GetValueOrDefault(day)))
            .ToList();

        return new TimelineAgentOverview(today, overviewYear, months, upcomingDaysLoad, pinnedOpenThisMonth, pendingTotal);
    }

    public async Task<IReadOnlyList<TimelineTaskInfo>> ListTasksAsync(Guid userId, TimelineTaskQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (from, to) = query.From <= query.To ? (query.From, query.To) : (query.To, query.From);
        var fromMonth = TimelineCalendar.MonthIndex(from.Year, from.Month);
        var toMonth = TimelineCalendar.MonthIndex(to.Year, to.Month);
        var includePinned = query.IncludePinned;

        var tasks = Tasks(userId).Where(t =>
            (t.Date != null && t.Date >= from && t.Date <= to)
            || (includePinned && t.Date == null && (t.Year * 12) + t.Month - 1 >= fromMonth && (t.Year * 12) + t.Month - 1 <= toMonth));

        if (query.Kind is { } kind)
        {
            tasks = tasks.Where(t => t.Kind == kind);
        }

        if (query.Status is { } status)
        {
            tasks = tasks.Where(t => t.Status == status);
        }

        if (TagNormalizer.NormalizeOne(query.Tag) is { } tag)
        {
            tasks = tasks.Where(t => t.Tags.Contains(tag));
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedAgent))
        {
            var agent = query.AssignedAgent.Trim().ToLowerInvariant();
            tasks = tasks.Where(t => t.AssignedAgent != null && t.AssignedAgent.ToLower() == agent);
        }

        var rows = await tasks
            .OrderBy(t => t.Date == null)
            .ThenBy(t => t.Date)
            .ThenBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ThenBy(t => t.CreatedAt)
            .Take(Math.Clamp(query.Limit, 1, TempusLimits.MaxListedTasks))
            .ToListAsync(ct);

        var today = await TodayAsync(userId, ct);

        // Day order from SQL; priority within a day/month in memory (priorities are stored by name).
        return [.. rows
            .GroupBy(t => (t.Date, t.Year, t.Month))
            .SelectMany(g => TaskRules.OrderForDisplay(g, t => t))
            .Select(t => TimelineMapping.ToInfo(t, today))];
    }

    public async Task<TimelineTaskDetail?> GetTaskAsync(Guid userId, Guid taskId, CancellationToken ct = default)
    {
        var task = await Tasks(userId).FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null)
        {
            return null;
        }

        var notes = await db.TimelineNotes
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.TaskId == taskId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(TempusLimits.MaxTaskNotes)
            .ToListAsync(ct);

        var pending = await Proposals(userId).CountAsync(p => p.TaskId == taskId && p.Status == TimelineProposalStatus.Pending, ct);

        return new TimelineTaskDetail(
            TimelineMapping.ToInfo(task, await TodayAsync(userId, ct)),
            task.Description,
            task.CompletedAt,
            [.. notes.Select(TimelineMapping.ToInfo)],
            pending);
    }

    public async Task<Result<TimelineProposalReceipt>> ProposeTaskAsync(Guid userId, string agentName, TimelineTaskProposal proposal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (userId == Guid.Empty)
        {
            return Result<TimelineProposalReceipt>.Invalid("No user is associated with this request.");
        }

        var title = proposal.Title?.Trim() ?? string.Empty;
        var tags = TagNormalizer.Normalize(proposal.Tags);
        var error = (title.Length == 0 ? "A task needs a title." : null)
            ?? ValidateCreate(title, proposal.Description, proposal.Rationale, proposal.AssignedAgent, tags)
            ?? TaskRules.ValidatePlacement(proposal.Year, proposal.Month, proposal.Date)
            ?? TaskRules.ValidateDueDate(proposal.DueDate);

        if (error is not null)
        {
            return Result<TimelineProposalReceipt>.Invalid(error);
        }

        var entity = new TimelineProposal
        {
            Kind = TimelineProposalKind.CreateTask,
            Title = title,
            Description = Blank(proposal.Description),
            Priority = proposal.Priority,
            Tags = tags,
            AssignedAgent = Blank(proposal.AssignedAgent),
            DueDate = proposal.DueDate,
            Year = proposal.Year,
            Month = proposal.Month,
            Date = proposal.Date,
            Rationale = Blank(proposal.Rationale)
        };

        var result = await db.Database.ExecuteInTransactionAsync(() => AddCreateProposalAsync(userId, agentName, entity, ct), ct: ct);
        await NotifyNewProposalAsync(userId, agentName, result, ct);
        return result;
    }

    public async Task<Result<TimelineProposalReceipt>> ProposeEventAsync(Guid userId, string agentName, TimelineEventProposal proposal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (userId == Guid.Empty)
        {
            return Result<TimelineProposalReceipt>.Invalid("No user is associated with this request.");
        }

        var title = proposal.Title?.Trim() ?? string.Empty;
        var tags = TagNormalizer.Normalize(proposal.Tags);
        var error = ValidateCreate(title, proposal.Description, proposal.Rationale, null, tags)
            ?? TaskRules.ValidatePlacement(proposal.Year, proposal.Month, proposal.Date)
            ?? TaskRules.ValidateTiming(proposal.Date, proposal.StartTime, proposal.EndDate);

        if (error is not null)
        {
            return Result<TimelineProposalReceipt>.Invalid(error);
        }

        var entity = new TimelineProposal
        {
            Kind = TimelineProposalKind.CreateEvent,
            Title = title,
            Description = Blank(proposal.Description),
            Tags = tags,
            Year = proposal.Year,
            Month = proposal.Month,
            Date = proposal.Date,
            StartTime = proposal.StartTime,
            EndDate = proposal.EndDate,
            Rationale = Blank(proposal.Rationale)
        };

        var result = await db.Database.ExecuteInTransactionAsync(() => AddCreateProposalAsync(userId, agentName, entity, ct), ct: ct);
        await NotifyNewProposalAsync(userId, agentName, result, ct);
        return result;
    }

    public async Task<Result<TimelineProposalReceipt>> ProposeRescheduleAsync(Guid userId, string agentName, TimelineRescheduleProposal proposal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (userId == Guid.Empty)
        {
            return Result<TimelineProposalReceipt>.Invalid("No user is associated with this request.");
        }

        var error = proposal.Rationale?.Length > TempusLimits.RationaleMax
            ? Limit("The rationale", TempusLimits.RationaleMax)
            : TaskRules.ValidatePlacement(proposal.Year, proposal.Month, proposal.Date);

        if (error is not null)
        {
            return Result<TimelineProposalReceipt>.Invalid(error);
        }

        var result = await db.Database.ExecuteInTransactionAsync(() => AddRescheduleProposalAsync(userId, agentName, proposal, ct), ct: ct);
        await NotifyNewProposalAsync(userId, agentName, result, ct);
        return result;
    }

    public async Task<Result<TimelineNoteReceipt>> AddNoteAsync(Guid userId, string agentName, TimelineNoteInput note, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (userId == Guid.Empty)
        {
            return Result<TimelineNoteReceipt>.Invalid("No user is associated with this request.");
        }

        var content = note.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
        {
            return Result<TimelineNoteReceipt>.Invalid("A note cannot be empty.");
        }

        if (content.Length > TempusLimits.NoteMax)
        {
            return Result<TimelineNoteReceipt>.Invalid(Limit("A note", TempusLimits.NoteMax));
        }

        if (!Enum.IsDefined(note.Kind))
        {
            return Result<TimelineNoteReceipt>.Invalid("Unknown note kind.");
        }

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var startOfDay = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var writtenToday = await db.TimelineNotes.CountAsync(n => n.UserId == userId && n.Author == TimelineAuthorKind.Agent && n.CreatedAt >= startOfDay, ct);
        if (writtenToday >= TempusLimits.MaxAgentNotesPerDay)
        {
            return Result<TimelineNoteReceipt>.Conflict(string.Create(CultureInfo.InvariantCulture, $"Agents can leave at most {TempusLimits.MaxAgentNotesPerDay} notes per day."));
        }

        var entity = new TimelineNote
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = note.Kind,
            Author = TimelineAuthorKind.Agent,
            AuthorName = AgentName(agentName),
            Content = content,
            CreatedAt = now
        };

        if (note.TaskId is { } taskId)
        {
            var task = await Tasks(userId).Where(t => t.Id == taskId).Select(t => new { t.Year, t.Month, t.Date }).FirstOrDefaultAsync(ct);
            if (task is null)
            {
                return Result<TimelineNoteReceipt>.NotFound($"No task with id {taskId} exists.");
            }

            (entity.TaskId, entity.Year, entity.Month, entity.Date) = (taskId, task.Year, task.Month, task.Date);
        }
        else if (note.Date is { } day)
        {
            if (!TempusLimits.IsValidYear(day.Year))
            {
                return Result<TimelineNoteReceipt>.Invalid(TaskRules.ValidatePlacement(day.Year, day.Month, day)!);
            }

            (entity.Year, entity.Month, entity.Date) = (day.Year, day.Month, day);
        }
        else
        {
            var (year, month) = note is { Year: { } y, Month: { } m } ? (y, m) : (today.Year, today.Month);
            if (TaskRules.ValidatePlacement(year, month, null) is { } placementError)
            {
                return Result<TimelineNoteReceipt>.Invalid(placementError);
            }

            (entity.Year, entity.Month) = (year, month);
        }

        db.TimelineNotes.Add(entity);
        await db.SaveChangesAsync(ct);

        return Result<TimelineNoteReceipt>.Success(new TimelineNoteReceipt(entity.Id, entity.Year, entity.Month, entity.Date, entity.TaskId));
    }

    /// <summary>
    /// Adds a CreateTask or CreateEvent proposal, unless the same one is already pending.
    /// </summary>
    private async Task<Result<TimelineProposalReceipt>> AddCreateProposalAsync(Guid userId, string agentName, TimelineProposal entity, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var lowered = entity.Title.ToLowerInvariant();
        var duplicate = await Proposals(userId)
            .Where(p => p.Status == TimelineProposalStatus.Pending
                && p.Kind == entity.Kind
                && p.Year == entity.Year
                && p.Month == entity.Month
                && p.Date == entity.Date
                && p.Title.ToLower() == lowered)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (duplicate is { } existingId)
        {
            return Result<TimelineProposalReceipt>.Success(new TimelineProposalReceipt(existingId, AlreadyPending: true, Superseded: 0));
        }

        if (await PendingLimitReachedAsync(userId, ct))
        {
            return TooManyPending<TimelineProposalReceipt>();
        }

        entity.Id = Guid.CreateVersion7();
        entity.UserId = userId;
        entity.Status = TimelineProposalStatus.Pending;
        entity.ProposedBy = AgentName(agentName);
        entity.CreatedAt = timeProvider.GetUtcNow();

        db.TimelineProposals.Add(entity);
        await db.SaveChangesAsync(ct);

        return Result<TimelineProposalReceipt>.Success(new TimelineProposalReceipt(entity.Id, AlreadyPending: false, Superseded: 0));
    }

    private async Task<Result<TimelineProposalReceipt>> AddRescheduleProposalAsync(Guid userId, string agentName, TimelineRescheduleProposal proposal, CancellationToken ct)
    {
        db.ChangeTracker.Clear();

        var task = await Tasks(userId).FirstOrDefaultAsync(t => t.Id == proposal.TaskId, ct);
        if (task is null)
        {
            return Result<TimelineProposalReceipt>.NotFound($"No task with id {proposal.TaskId} exists.");
        }

        if (task.Kind == TimelineEntryKind.Task && task.Status == TimelineTaskStatus.Done)
        {
            return Result<TimelineProposalReceipt>.Invalid("The task is already done.");
        }

        if (TaskRules.IsAt(task, proposal.Year, proposal.Month, proposal.Date))
        {
            return Result<TimelineProposalReceipt>.Invalid("The entry is already planned there.");
        }

        var now = timeProvider.GetUtcNow();

        // The newest suggestion for a task replaces the older ones (they would conflict on accept anyway).
        var superseded = await TaskRules.SupersedePendingReschedulesAsync(db, task.Id, now, keepProposalId: null, ct);

        if (await PendingLimitReachedAsync(userId, ct))
        {
            return TooManyPending<TimelineProposalReceipt>();
        }

        var entity = new TimelineProposal
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = TimelineProposalKind.Reschedule,
            Status = TimelineProposalStatus.Pending,
            TaskId = task.Id,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Tags = [.. task.Tags],
            AssignedAgent = task.AssignedAgent,
            DueDate = task.DueDate,
            StartTime = task.StartTime,
            EndDate = task.EndDate,
            Year = proposal.Year,
            Month = proposal.Month,
            Date = proposal.Date,
            FromYear = task.Year,
            FromMonth = task.Month,
            FromDate = task.Date,
            Rationale = Blank(proposal.Rationale),
            ProposedBy = AgentName(agentName),
            CreatedAt = now
        };

        db.TimelineProposals.Add(entity);
        await db.SaveChangesAsync(ct);

        return Result<TimelineProposalReceipt>.Success(new TimelineProposalReceipt(entity.Id, AlreadyPending: false, superseded));
    }

    /// <summary>
    /// Runs after the proposal's transaction committed, so a retried transaction never notifies twice.
    /// </summary>
    private async Task NotifyNewProposalAsync(Guid userId, string agentName, Result<TimelineProposalReceipt> result, CancellationToken ct)
    {
        if (result is not { IsSuccess: true, Value: { AlreadyPending: false } receipt })
        {
            return;
        }

        var proposal = await Proposals(userId)
            .Where(p => p.Id == receipt.ProposalId)
            .Select(p => new { p.Kind, p.Title, p.Year, p.Month, p.Rationale })
            .FirstOrDefaultAsync(ct);

        if (proposal is null)
        {
            return;
        }

        var verb = proposal.Kind switch
        {
            TimelineProposalKind.Reschedule => "moving",
            TimelineProposalKind.CreateEvent => "adding the event",
            _ => "adding"
        };
        var message = new NotificationMessage(
            NotificationCategories.TimelineProposal,
            NotificationSeverity.Info,
            $"{AgentName(agentName)} suggests {verb} “{proposal.Title}”",
            proposal.Rationale,
            string.Create(CultureInfo.InvariantCulture, $"/timeline?year={proposal.Year}&month={proposal.Month}"),
            ProposalDedupeKey);

        await notifier.NotifyAsync(NotificationRecipient.User(userId), message, ct);
    }

    private async Task<DateOnly> TodayAsync(Guid userId, CancellationToken ct)
        => (await calendars.GetAsync(userId, ct)).Today(timeProvider.GetUtcNow());

    private static string? ValidateCreate(string title, string? description, string? rationale, string? assignedAgent, List<string> tags)
    {
        return title.Length == 0 ? "A title is required."
            : title.Length > TempusLimits.TitleMax ? Limit("The title", TempusLimits.TitleMax)
            : description?.Length > TempusLimits.DescriptionMax ? Limit("The description", TempusLimits.DescriptionMax)
            : rationale?.Length > TempusLimits.RationaleMax ? Limit("The rationale", TempusLimits.RationaleMax)
            : assignedAgent?.Trim().Length > TempusLimits.AgentNameMax ? Limit("The agent name", TempusLimits.AgentNameMax)
            : tags.Count > TempusLimits.MaxTagsPerTask ? string.Create(CultureInfo.InvariantCulture, $"An entry can have at most {TempusLimits.MaxTagsPerTask} tags.")
            : tags.Any(t => t.Length > TempusLimits.TagMax || t.Contains(',', StringComparison.Ordinal)) ? string.Create(CultureInfo.InvariantCulture, $"Tags can be at most {TempusLimits.TagMax} characters and cannot contain commas.")
            : null;
    }

    private IQueryable<TimelineTask> Tasks(Guid userId) => db.TimelineTasks.AsNoTracking().Where(t => t.UserId == userId);

    private IQueryable<TimelineProposal> Proposals(Guid userId) => db.TimelineProposals.AsNoTracking().Where(p => p.UserId == userId);

    private async Task<bool> PendingLimitReachedAsync(Guid userId, CancellationToken ct)
        => await Proposals(userId).CountAsync(p => p.Status == TimelineProposalStatus.Pending, ct) >= TempusLimits.MaxPendingProposals;

    private static Result<T> TooManyPending<T>()
    {
        return Result<T>.Conflict(string.Create(
            CultureInfo.InvariantCulture,
            $"The user already has {TempusLimits.MaxPendingProposals} suggestions waiting for review; wait until they answer some."));
    }

    private static string Limit(string what, int max) => string.Create(CultureInfo.InvariantCulture, $"{what} can be at most {max} characters.");

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string AgentName(string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "Agent" : name.Trim();
        return trimmed.Length <= TempusLimits.AgentNameMax ? trimmed : trimmed[..TempusLimits.AgentNameMax];
    }
}
