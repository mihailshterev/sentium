using AgentRuntime.Core.Dtos;
using AgentRuntime.Core.WorkflowManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Authorize]
[Route("workflows")]
public sealed class WorkflowsController(IWorkflowService workflowService, IWorkflowRunRepository runRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetWorkflows(CancellationToken ct)
    {
        var workflows = await workflowService.GetWorkflowsAsync(ct);
        return Ok(workflows);
    }

    [HttpGet("runs")]
    public async Task<IActionResult> GetWorkflowRuns([FromQuery] int count = 20, CancellationToken ct = default)
    {
        var runs = await runRepository.GetRecentAsync(Math.Min(count, 100), ct);
        return Ok(runs);
    }

    [HttpGet("{workflowId:guid}")]
    public async Task<IActionResult> GetWorkflow(Guid workflowId, CancellationToken ct)
    {
        var workflow = await workflowService.GetWorkflowAsync(workflowId, ct);
        return Ok(workflow);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkflow([FromBody] CreateWorkflowRequest request, CancellationToken ct)
    {
        var result = await workflowService.CreateWorkflowAsync(request, ct);
        return CreatedAtAction(nameof(GetWorkflow), new { workflowId = result.Id }, result);
    }

    [HttpPut("{workflowId:guid}")]
    public async Task<IActionResult> UpdateWorkflow(Guid workflowId, [FromBody] UpdateWorkflowRequest request, CancellationToken ct)
    {
        await workflowService.UpdateWorkflowAsync(workflowId, request, ct);
        return NoContent();
    }

    [HttpDelete("{workflowId:guid}")]
    public async Task<IActionResult> DeleteWorkflow(Guid workflowId, CancellationToken ct)
    {
        await workflowService.DeleteWorkflowAsync(workflowId, ct);
        return NoContent();
    }
}
