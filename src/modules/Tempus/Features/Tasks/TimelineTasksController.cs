using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Results;

namespace Sentium.Tempus.Features.Tasks;

/// <summary>
/// The signed-in user's timeline Entries: Tasks and Calendar Events.
/// </summary>
[ApiController]
[Authorize]
[Route($"{TempusRoutes.Prefix}/tasks")]
internal sealed class TimelineTasksController(ITimelineTaskService taskService) : ControllerBase
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
    /// Adds a Task or Calendar Event on a day, or pinned to a month when no date is given. With a repeat rule it
    /// creates the Series and returns its first occurrence.
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

    /// <param name="scope">For an occurrence: <c>One</c> (default) or <c>Following</c>.</param>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(Guid id, [FromBody] SaveTimelineTaskRequest request, [FromQuery] TimelineEditScope scope = TimelineEditScope.One, CancellationToken ct = default)
        => (await taskService.UpdateAsync(id, request, scope, ct)).ToActionResult(this);

    /// <summary>
    /// Moves an Entry to another day, or pins it to a month. An occurrence is detached from its Series.
    /// </summary>
    [HttpPut("{id:guid}/schedule")]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScheduleTask(Guid id, [FromBody] ScheduleTimelineTaskRequest request, CancellationToken ct)
        => (await taskService.ScheduleAsync(id, request, ct)).ToActionResult(this);

    /// <summary>
    /// Ticks or unticks a Task; a Calendar Event is refused.
    /// </summary>
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
    /// Deletes an Entry with its notes and pending proposals. <c>Following</c> also deletes the later occurrences of
    /// its Series that are neither done nor past.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id, [FromQuery] TimelineEditScope scope = TimelineEditScope.One, CancellationToken ct = default)
        => await taskService.DeleteAsync(id, scope, ct) ? NoContent() : NotFound();
}
