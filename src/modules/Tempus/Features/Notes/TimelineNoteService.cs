using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Tempus.Common;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Data;
using Sentium.Tempus.Entities;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Notes;

public sealed class TimelineNoteService(TempusDbContext db, TimeProvider timeProvider) : ITimelineNoteService
{
    public async Task<Result<TimelineNoteDto>> CreateAsync(CreateTimelineNoteRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result<TimelineNoteDto>.Invalid("A note cannot be empty.");
        }

        var note = new TimelineNote
        {
            Id = Guid.CreateVersion7(),
            Kind = TimelineNoteKind.Note,
            Author = TimelineAuthorKind.User,
            Content = request.Content.Trim(),
            CreatedAt = timeProvider.GetUtcNow()
        };

        if (request.TaskId is { } taskId)
        {
            var task = await db.TimelineTasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new { t.Year, t.Month, t.Date })
                .FirstOrDefaultAsync(ct);

            if (task is null)
            {
                return Result<TimelineNoteDto>.Invalid("The task does not exist.");
            }

            (note.TaskId, note.Year, note.Month, note.Date) = (taskId, task.Year, task.Month, task.Date);
        }
        else
        {
            if (TaskRules.ValidatePlacement(request.Year, request.Month, request.Date) is { } error)
            {
                return Result<TimelineNoteDto>.Invalid(error);
            }

            (note.Year, note.Month, note.Date) = (request.Year, request.Month, request.Date);
        }

        db.TimelineNotes.Add(note);
        await db.SaveChangesAsync(ct);

        return Result<TimelineNoteDto>.Success(TimelineMapping.ToDto(note));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => await db.TimelineNotes.Where(n => n.Id == id).ExecuteDeleteAsync(ct) > 0;
}
