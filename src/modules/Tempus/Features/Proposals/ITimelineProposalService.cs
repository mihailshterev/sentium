using Sentium.Core.Results;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Proposals;

internal interface ITimelineProposalService
{
    /// <param name="status">Which proposals to list; pending ones when <see langword="null"/>.</param>
    Task<IReadOnlyList<TimelineProposalDto>> ListAsync(TimelineProposalStatus? status, CancellationToken ct = default);

    /// <summary>
    /// Applies an agent's proposal: adds the suggested task, or moves the task. A proposal whose task changed since
    /// (moved, completed or deleted) is marked superseded and reported as a conflict.
    /// </summary>
    Task<Result<TimelineTaskDto>> AcceptAsync(Guid id, CancellationToken ct = default);

    Task<Result<TimelineProposalDto>> RejectAsync(Guid id, CancellationToken ct = default);
}
