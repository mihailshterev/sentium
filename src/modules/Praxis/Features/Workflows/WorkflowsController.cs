using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.WorkflowManagement;

namespace Sentium.Praxis.Features.Workflows;

[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/workflows")]
internal sealed class WorkflowsController(IWorkflowService workflowService, IWorkflowRunRepository runRepository) : ControllerBase
{
    /// <summary>
    /// Returns a page of workflows (newest first).
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<WorkflowResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkflowResponse>>> GetWorkflows(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var workflows = await workflowService.GetWorkflowsPagedAsync(page, pageSize, ct);
        return Ok(workflows);
    }

    /// <summary>
    /// Returns a page of workflow runs, ordered by start time descending.
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of runs per page (default: 20, max: 100).</param>
    /// <param name="search">Optional text matched against the run title, its workflow name, or (untitled runs) the scenario.</param>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(PagedResponse<WorkflowRunSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkflowRunSummaryResponse>>> GetWorkflowRuns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();

        var (runs, total) = await runRepository.GetPagedAsync(page, pageSize, search, ct);
        return Ok(PagedResponse<WorkflowRunSummaryResponse>.Create(runs, total, page, pageSize));
    }

    /// <summary>
    /// Returns a single workflow run by its ID, including its full log history.
    /// </summary>
    /// <returns>The workflow run if found; otherwise, a 404 Not Found response.</returns>
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowRunResponse>> GetWorkflowRun(Guid runId, CancellationToken ct = default)
    {
        var run = await runRepository.GetByIdAsync(runId, ct);
        return run is null ? NotFound() : Ok(run);
    }

    /// <returns>No content, or 404 Not Found when the run does not exist.</returns>
    [HttpPut("runs/{runId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenameWorkflowRun(Guid runId, [FromBody] UpdateTitleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var renamed = await runRepository.RenameAsync(runId, request.Title.Trim(), ct);
        return renamed ? NoContent() : NotFound();
    }

    /// <returns>The workflow details if found; otherwise, a 404 Not Found response.</returns>
    [HttpGet("{workflowId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowResponse>> GetWorkflow(Guid workflowId, CancellationToken ct)
    {
        var workflow = await workflowService.GetWorkflowAsync(workflowId, ct);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    /// <summary>
    /// Creates a new workflow based on the provided configuration and returns the created workflow details.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkflowResponse>> CreateWorkflow([FromBody] CreateWorkflowRequest request, CancellationToken ct)
    {
        var result = await workflowService.CreateWorkflowAsync(request, ct);
        return CreatedAtAction(nameof(GetWorkflow), new { workflowId = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing workflow with the provided configuration.
    /// </summary>
    [HttpPut("{workflowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkflow(Guid workflowId, [FromBody] UpdateWorkflowRequest request, CancellationToken ct)
    {
        var updated = await workflowService.UpdateWorkflowAsync(workflowId, request, ct);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{workflowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkflow(Guid workflowId, CancellationToken ct)
    {
        var deleted = await workflowService.DeleteWorkflowAsync(workflowId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
