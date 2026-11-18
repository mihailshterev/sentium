using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;

namespace Sentium.Probatio.Features.Evaluations;

/// <summary>
/// The caller's own Evaluations: start, watch (poll), cancel and review. Tools are never executed (ADR 0003).
/// </summary>
[ApiController]
[Authorize]
[Route($"{ProbatioRoutes.Prefix}/evaluations")]
internal sealed class EvaluationsController(IEvaluationService evaluationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<EvaluationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluations(
        [FromQuery] Guid? suiteId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        return Ok(await evaluationService.GetPagedAsync(suiteId, page, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvaluationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvaluation(Guid id, CancellationToken ct)
        => await evaluationService.GetAsync(id, ct) is { } evaluation ? Ok(evaluation) : NotFound();

    /// <summary>
    /// Queues an Evaluation; it is worked on in the background.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EvaluationSummaryDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartEvaluation([FromBody] StartEvaluationRequest request, CancellationToken ct)
    {
        var result = await evaluationService.StartAsync(request, ct);
        return result.IsSuccess
            ? AcceptedAtAction(nameof(GetEvaluation), new { id = result.Value!.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <response code="409">The Evaluation has already finished.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(EvaluationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelEvaluation(Guid id, CancellationToken ct)
        => (await evaluationService.CancelAsync(id, ct)).ToActionResult(this);

    /// <response code="409">The Evaluation is still queued or running.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvaluation(Guid id, CancellationToken ct)
    {
        var result = await evaluationService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult(this);
    }
}
