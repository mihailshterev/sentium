using Microsoft.EntityFrameworkCore;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features;

namespace Sentium.Tempus.Testing;

/// <summary>
/// Baseline timeline for the Playwright suite (Testing environment only): a few tasks in the current month and one
/// agent note. Proposals are not seeded - every browser project shares the seeded user, so tests that accept or
/// reject proposals mock them instead.
/// </summary>
public static class TempusE2EDataSeeder
{
    public const string TodayTaskTitle = "e2e-baseline-today";
    public const string PinnedTaskTitle = "e2e-baseline-pinned";
    public const string DoneTaskTitle = "e2e-baseline-done";
    public const string AgentNoteContent = "e2e-baseline progress report";

    public static async Task SeedAsync(TempusDbContext db, Guid userId, TimeProvider timeProvider, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (await db.TimelineTasks.AnyAsync(t => t.UserId == userId && t.Title == TodayTaskTitle, ct))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var today = TimelineCalendar.UtcToday(timeProvider);

        db.TimelineTasks.AddRange(
            NewTask(userId, TodayTaskTitle, today, today, TimelineTaskStatus.Todo, TimelineTaskPriority.High, now),
            NewTask(userId, PinnedTaskTitle, today, date: null, TimelineTaskStatus.InProgress, TimelineTaskPriority.Normal, now),
            NewTask(userId, DoneTaskTitle, today, TimelineCalendar.FirstDay(today.Year, today.Month), TimelineTaskStatus.Done, TimelineTaskPriority.Low, now));

        db.TimelineNotes.Add(new TimelineNote
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Year = today.Year,
            Month = today.Month,
            Kind = TimelineNoteKind.ProgressReport,
            Author = TimelineAuthorKind.Agent,
            AuthorName = "TimelinePlanner",
            Content = AgentNoteContent,
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
    }

    private static TimelineTask NewTask(
        Guid userId,
        string title,
        DateOnly month,
        DateOnly? date,
        TimelineTaskStatus status,
        TimelineTaskPriority priority,
        DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Title = title,
            Year = month.Year,
            Month = month.Month,
            Date = date,
            Priority = priority,
            Status = status,
            CompletedAt = status == TimelineTaskStatus.Done ? now : null,
            Tags = ["e2e"],
            Origin = TimelineAuthorKind.User,
            CreatedAt = now,
            UpdatedAt = now
        };
}
