using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Audit;
using Sentium.Sentinel.Features.Policies;

namespace Sentium.Sentinel.Features.Engine;

/// <summary>
/// Policy Decision Point (PDP) API. Modules inside Sentium.Web evaluate in-process through
/// <see cref="IPolicyDecisionPoint"/>; the HTTP evaluate endpoint exists for the out-of-process Sandbox.
/// </summary>
[ApiController]
[Route($"{SentinelRoutes.Prefix}/policy")]
internal sealed class PolicyController(IPolicyDecisionPoint policyDecisionPoint, IAuditLog auditLog) : ControllerBase
{
    /// <summary>
    /// Evaluates a policy request and returns an authorization decision.
    /// Restricted to internal service callers (the Sandbox) via the <c>SystemCaller</c> authorization policy.
    /// </summary>
    [HttpPost("evaluate")]
    [AuthorizeSystem]
    [ProducesResponseType<PolicyEvaluationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyEvaluationResponse>> EvaluateAsync([FromBody] PolicyEvaluationRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        return Ok(await policyDecisionPoint.EvaluateAsync(body, ct));
    }

    /// <summary>
    /// Returns a page of forensic audit records, newest first. Requires authentication.
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of records per page (default: 20, max: 100).</param>
    [HttpGet("audit")]
    [AuthorizeSovereign]
    [ProducesResponseType<PagedResponse<AuditRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAudit(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();

        var (records, total) = await auditLog.GetPagedAsync(page, pageSize, ct);
        return Ok(PagedResponse<AuditRecord>.Create(records, total, page, pageSize));
    }

    /// <summary>
    /// Returns a page of audit records for a specific agent. Requires authentication.
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of records per page (default: 20, max: 100).</param>
    [HttpGet("audit/agent/{agentId}")]
    [AuthorizeSovereign]
    [ProducesResponseType<PagedResponse<AuditRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditByAgent(
        string agentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationQuery.DefaultPageSize,
        CancellationToken ct = default)
    {
        var query = new PaginationQuery { Page = page, PageSize = pageSize };
        (page, pageSize) = query.Normalize();

        var (records, total) = await auditLog.GetByAgentPagedAsync(agentId, page, pageSize, ct);
        return Ok(PagedResponse<AuditRecord>.Create(records, total, page, pageSize));
    }

    /// <summary>
    /// Returns aggregate statistics for the current audit window. Requires authentication.
    /// </summary>
    [HttpGet("audit/stats")]
    [AuthorizeSovereign]
    [ProducesResponseType<AuditStatsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditStats(CancellationToken ct = default)
    {
        var records = await auditLog.GetRecentAsync(500, ct);

        var latestAlignment = records
            .Where(r => r.AlignmentVerdict is not null)
            .Take(20)
            .Select(r => r.AlignmentVerdict switch
            {
                "Aligned" => 1.0,
                "Inconclusive" => 0.5,
                "Misaligned" => 0.0,
                _ => (double?)null
            })
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        double? avgAlignment = latestAlignment.Count > 0 ? latestAlignment.Average() : null;

        var stats = new AuditStatsDto
        {
            Total = records.Count,
            Allowed = records.Count(r => r.Allowed),
            Denied = records.Count(r => !r.Allowed),
            Alerts = records.Count(r => r.Effect == PolicyEffect.DenyWithAlert),
            LowRisk = records.Count(r => r.Risk == PolicyRiskLevel.Low),
            MediumRisk = records.Count(r => r.Risk == PolicyRiskLevel.Medium),
            HighRisk = records.Count(r => r.Risk == PolicyRiskLevel.High),
            CriticalRisk = records.Count(r => r.Risk == PolicyRiskLevel.Critical),
            LatestAlignmentScore = avgAlignment
        };

        return Ok(stats);
    }

}
