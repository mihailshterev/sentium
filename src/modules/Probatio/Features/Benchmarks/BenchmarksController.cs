using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;

namespace Sentium.Probatio.Features.Benchmarks;

/// <summary>
/// The caller's own Benchmarks: one Evaluation per model of the same suite version and agent, compared side by side.
/// </summary>
[ApiController]
[Authorize]
[Route($"{ProbatioRoutes.Prefix}/benchmarks")]
internal sealed class BenchmarksController(IBenchmarkService benchmarkService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<BenchmarkSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBenchmarks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        return Ok(await benchmarkService.GetPagedAsync(page, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BenchmarkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBenchmark(Guid id, CancellationToken ct)
        => await benchmarkService.GetAsync(id, ct) is { } benchmark ? Ok(benchmark) : NotFound();

    /// <summary>
    /// Queues one Evaluation per model; they are worked on in the background, one after another.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BenchmarkSummaryDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartBenchmark([FromBody] StartBenchmarkRequest request, CancellationToken ct)
    {
        var result = await benchmarkService.StartAsync(request, ct);
        return result.IsSuccess
            ? AcceptedAtAction(nameof(GetBenchmark), new { id = result.Value!.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <response code="409">Every Evaluation of the Benchmark has already finished.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BenchmarkSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelBenchmark(Guid id, CancellationToken ct)
        => (await benchmarkService.CancelAsync(id, ct)).ToActionResult(this);

    /// <response code="409">An Evaluation of the Benchmark is still queued or running.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBenchmark(Guid id, CancellationToken ct)
    {
        var result = await benchmarkService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult(this);
    }
}
