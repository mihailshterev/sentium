using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Results;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// The signed-in user's timeline tasks.
/// </summary>
[ApiController]
[Authorize]
[Route($"{TempusRoutes.Prefix}/tasks")]
public sealed class TimelineTasksController(ITimelineTaskService taskService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTask(Guid id, CancellationToken ct)
    {
        var task = await taskService.GetAsync(id, ct);
        return task is null ? NotFound() : Ok(task);
    }

    /// <summary>
    /// Adds a task on a day, or pinned to a month when no date is given.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTask([FromBody] SaveTimelineTaskRequest request, CancellationToken ct)
    {
        var result = await taskService.CreateAsync(request, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTask), new { id = result.Value!.Id }, result.Value)
            : result.ToActionResult(this);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(Guid id, [FromBody] SaveTimelineTaskRequest request, CancellationToken ct)
        => (await taskService.UpdateAsync(id, request, ct)).ToActionResult(this);

    /// <summary>
    /// Moves a task to another day, or pins it to a month.
    /// </summary>
    [HttpPut("{id:guid}/schedule")]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScheduleTask(Guid id, [FromBody] ScheduleTimelineTaskRequest request, CancellationToken ct)
        => (await taskService.ScheduleAsync(id, request, ct)).ToActionResult(this);

    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTaskStatus(Guid id, [FromBody] SetTimelineTaskStatusRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (await taskService.SetStatusAsync(id, request.Status, ct)).ToActionResult(this);
    }

    /// <summary>
    /// Deletes a task with its notes and pending proposals.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken ct)
        => await taskService.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
