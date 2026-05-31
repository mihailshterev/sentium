using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.WorkflowManagement;
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
    /// Returns a list of all workflows
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of workflows</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowResponse>>> GetWorkflows(CancellationToken ct)
    {
        var workflows = await workflowService.GetWorkflowsAsync(ct);
        return Ok(workflows);
    }

    /// <summary>
    /// Returns a list of recent workflow runs, ordered by start time descending. The count parameter limits the number of runs returned, with a maximum of 100.
    /// </summary>
    /// <param name="count">The maximum number of workflow runs to return (default is 20, maximum is 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of recent workflow runs.</returns>
    [HttpGet("runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkflowRunResponse>>> GetWorkflowRuns([FromQuery] int count = 20, CancellationToken ct = default)
    {
        var runs = await runRepository.GetRecentAsync(Math.Min(count, 100), ct);
        return Ok(runs);
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
        return Ok(workflow);
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
    public async Task<IActionResult> UpdateWorkflow(Guid workflowId, [FromBody] UpdateWorkflowRequest request, CancellationToken ct)
    {
        await workflowService.UpdateWorkflowAsync(workflowId, request, ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes an existing workflow by its ID.
    /// </summary>
    /// <param name="workflowId">The ID of the workflow to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content if the deletion is successful.</returns>
    [HttpDelete("{workflowId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteWorkflow(Guid workflowId, CancellationToken ct)
    {
        await workflowService.DeleteWorkflowAsync(workflowId, ct);
        return NoContent();
    }
}
