using Sentium.Core.Results;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Tasks;

internal interface ITimelineTaskService
{
    Task<TimelineTaskDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a Task or Calendar Event; with a repeat rule it creates the Series and its occurrences a year ahead
    /// and returns the first one.
    /// </summary>
    Task<Result<TimelineTaskDto>> CreateAsync(SaveTimelineTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edits an Entry; its kind never changes. A one-off Entry given a repeat rule starts a Series. For an
    /// occurrence, <paramref name="scope"/> picks this one (detached) or this and all following.
    /// </summary>
    Task<Result<TimelineTaskDto>> UpdateAsync(Guid id, SaveTimelineTaskRequest request, TimelineEditScope scope = TimelineEditScope.One, CancellationToken ct = default);

    /// <summary>
    /// Moves an Entry to a day or pins it to a month. Its notes follow it, pending agent moves for it are
    /// superseded, an occurrence is detached from its Series and the Reminder is re-armed.
    /// </summary>
    Task<Result<TimelineTaskDto>> ScheduleAsync(Guid id, ScheduleTimelineTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Ticks or unticks a Task. A Calendar Event has no status and is <see cref="ResultStatus.Invalid"/>.
    /// </summary>
    Task<Result<TimelineTaskDto>> SetStatusAsync(Guid id, TimelineTaskStatus status, CancellationToken ct = default);

    /// <summary>
    /// Deletes an Entry with its notes and proposals. Deleting one occurrence keeps the Series from recreating it;
    /// deleting this and all following also removes later occurrences, except ones already done or past.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, TimelineEditScope scope = TimelineEditScope.One, CancellationToken ct = default);
}
