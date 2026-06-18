using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
using Sentium.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller for managing agent workflows.
/// </summary>
[ApiController]
[Authorize]
[Route("workflows")]
public sealed class WorkflowsController(IWorkflowService workflowService, IWorkflowRunRepository runRepository) : ControllerBase
{
    /// <summary>
    /// Returns a page of workflows (newest first).
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A paginated list of workflows.</returns>
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
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of workflow runs.</returns>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(PagedResponse<WorkflowRunSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<WorkflowRunSummaryResponse>>> GetWorkflowRuns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();

        var (runs, total) = await runRepository.GetPagedAsync(page, pageSize, ct);
        return Ok(PagedResponse<WorkflowRunSummaryResponse>.Create(runs, total, page, pageSize));
    }

    /// <summary>
    /// Returns a single workflow run by its ID, including its full log history.
    /// </summary>
    /// <param name="runId">The ID of the workflow run to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The workflow run if found; otherwise, a 404 Not Found response.</returns>
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowRunResponse>> GetWorkflowRun(Guid runId, CancellationToken ct = default)
    {
        var run = await runRepository.GetByIdAsync(runId, ct);
        return run is null ? NotFound() : Ok(run);
    }

    /// <summary>
    /// Returns details of a specific workflow by its ID.
    /// </summary>
    /// <param name="workflowId">The ID of the workflow to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
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
    /// <param name="request">The workflow creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created workflow details.</returns>
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
    /// <param name="workflowId">The ID of the workflow to update.</param>
    /// <param name="request">The workflow update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if the update is successful.</returns>
    [HttpPut("{workflowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkflow(Guid workflowId, [FromBody] UpdateWorkflowRequest request, CancellationToken ct)
    {
        var updated = await workflowService.UpdateWorkflowAsync(workflowId, request, ct);
        return updated ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes an existing workflow by its ID.
    /// </summary>
    /// <param name="workflowId">The ID of the workflow to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if the deletion is successful.</returns>
    [HttpDelete("{workflowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkflow(Guid workflowId, CancellationToken ct)
    {
        var deleted = await workflowService.DeleteWorkflowAsync(workflowId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
