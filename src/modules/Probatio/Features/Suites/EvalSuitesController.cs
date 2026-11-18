using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Results;

namespace Sentium.Probatio.Features.Suites;

/// <summary>
/// The caller's own Eval Suites. Every edit that changes the cases creates a new version; earlier versions stay
/// readable.
/// </summary>
[ApiController]
[Authorize]
[Route($"{ProbatioRoutes.Prefix}/suites")]
internal sealed class EvalSuitesController(IEvalSuiteService suiteService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<EvalSuiteSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        return Ok(await suiteService.GetPagedAsync(page, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvalSuiteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSuite(Guid id, CancellationToken ct)
        => await suiteService.GetAsync(id, ct) is { } suite ? Ok(suite) : NotFound();

    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<EvalSuiteVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersions(Guid id, CancellationToken ct)
        => await suiteService.GetVersionsAsync(id, ct) is { } versions ? Ok(versions) : NotFound();

    [HttpGet("{id:guid}/versions/{version:int}")]
    [ProducesResponseType(typeof(EvalSuiteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(Guid id, int version, CancellationToken ct)
        => await suiteService.GetVersionAsync(id, version, ct) is { } suite ? Ok(suite) : NotFound();

    [HttpPost]
    [ProducesResponseType(typeof(EvalSuiteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSuite([FromBody] CreateEvalSuiteRequest request, CancellationToken ct)
    {
        var suite = await suiteService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetSuite), new { id = suite.Id }, suite);
    }

    /// <summary>
    /// Saves the whole suite.
    /// </summary>
    /// <response code="409">The suite moved past the edit's base version.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EvalSuiteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSuite(Guid id, [FromBody] UpdateEvalSuiteRequest request, CancellationToken ct)
        => (await suiteService.UpdateAsync(id, request, ct)).ToActionResult(this);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSuite(Guid id, CancellationToken ct)
        => await suiteService.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
