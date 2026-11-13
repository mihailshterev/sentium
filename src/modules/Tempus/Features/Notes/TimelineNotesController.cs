using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Results;
using Sentium.Tempus.Features.Notes;

namespace Sentium.Tempus.Features.Notes;

/// <summary>
/// Notes on the timeline. Users write notes here; agents leave theirs through their tools.
/// </summary>
[ApiController]
[Authorize]
[Route($"{TempusRoutes.Prefix}/notes")]
public sealed class TimelineNotesController(ITimelineNoteService noteService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TimelineNoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNote([FromBody] CreateTimelineNoteRequest request, CancellationToken ct)
        => (await noteService.CreateAsync(request, ct)).ToActionResult(this);

    /// <summary>
    /// Removes a note (the user's own or an agent's).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct)
        => await noteService.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
