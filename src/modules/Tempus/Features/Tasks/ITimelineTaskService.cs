using Sentium.Core.Results;
using Sentium.Tempus.Contracts.Models;

namespace Sentium.Tempus.Features.Tasks;

internal interface ITimelineTaskService
{
    Task<TimelineTaskDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<Result<TimelineTaskDto>> CreateAsync(SaveTimelineTaskRequest request, CancellationToken ct = default);

    Task<Result<TimelineTaskDto>> UpdateAsync(Guid id, SaveTimelineTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Moves a task to a day or pins it to a month. Its notes follow it; pending agent moves for it are superseded.
    /// </summary>
    Task<Result<TimelineTaskDto>> ScheduleAsync(Guid id, ScheduleTimelineTaskRequest request, CancellationToken ct = default);

    Task<Result<TimelineTaskDto>> SetStatusAsync(Guid id, TimelineTaskStatus status, CancellationToken ct = default);

    /// <summary>
    /// Deletes a task with its notes and proposals.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
