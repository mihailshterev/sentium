using Sentium.Core.Results;

namespace Sentium.Tempus.Features.Notes;

internal interface ITimelineNoteService
{
    Task<Result<TimelineNoteDto>> CreateAsync(CreateTimelineNoteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Removes a note - the user's own, or one an agent left.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
