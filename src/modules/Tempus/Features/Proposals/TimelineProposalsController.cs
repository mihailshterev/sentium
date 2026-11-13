using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Results;
using Sentium.Tempus.Contracts.Models;
using Sentium.Tempus.Features.Proposals;
using Sentium.Tempus.Features.Tasks;

namespace Sentium.Tempus.Features.Proposals;

/// <summary>
/// Tasks and moves agents suggested, waiting for the user's answer.
/// </summary>
[ApiController]
[Authorize]
[Route($"{TempusRoutes.Prefix}/proposals")]
public sealed class TimelineProposalsController(ITimelineProposalService proposalService) : ControllerBase
{
    /// <param name="status">Which proposals to list (default <c>Pending</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TimelineProposalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProposals([FromQuery] TimelineProposalStatus? status, CancellationToken ct)
        => Ok(await proposalService.ListAsync(status, ct));

    /// <summary>
    /// Accepts a proposal and returns the added or moved task.
    /// </summary>
    /// <response code="409">The proposal is no longer pending, or its task changed since it was made.</response>
    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(TimelineTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptProposal(Guid id, CancellationToken ct)
        => (await proposalService.AcceptAsync(id, ct)).ToActionResult(this);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(TimelineProposalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectProposal(Guid id, CancellationToken ct)
        => (await proposalService.RejectAsync(id, ct)).ToActionResult(this);
}
