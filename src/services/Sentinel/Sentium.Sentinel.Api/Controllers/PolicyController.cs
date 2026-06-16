using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Dtos;
using Sentium.Sentinel.Core.Policies;
using Sentium.Shared.Results;

namespace Sentium.Sentinel.Api.Controllers;

/// <summary>
/// Policy Decision Point (PDP) API.
/// </summary>
[ApiController]
[Route("policy")]
public sealed class PolicyController(
    SentinelPolicyEngine engine,
    IAuditLog auditLog) : ControllerBase
{
    /// <summary>
    /// Evaluates a policy request and returns an authorization decision.
    /// Restricted to internal service callers via the <c>SystemCaller</c> authorization policy.
    /// </summary>
    [HttpPost("evaluate")]
    [AuthorizeSystem]
    [ProducesResponseType<PolicyEvaluationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EvaluateAsync([FromBody] PolicyEvaluationRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body, nameof(body));

        var resourceType = Enum.Parse<ResourceType>(body.ResourceType, ignoreCase: true);

        var request = new PolicyRequest
        {
            AgentId = body.AgentId,
            SkillName = body.SkillName,
            ResourceType = resourceType,
            ResourceId = body.ResourceId,
            Action = body.Action,
            OriginalUserPrompt = body.OriginalUserPrompt,
            CorrelationId = body.CorrelationId,
            Metadata = body.Metadata
        };

        var decision = await engine.EvaluateAsync(request, ct);

        var response = new PolicyEvaluationResponse
        {
            Allowed = decision.Allowed,
            Effect = decision.Effect.ToString(),
            Reason = decision.Reason,
            Risk = decision.Risk.ToString(),
            AuditId = decision.AuditId,
            Timestamp = decision.Timestamp,
            TriggeredPolicies = decision.TriggeredPolicies
        };

        return Ok(response);
    }

    /// <summary>
    /// Returns a page of forensic audit records, newest first. Requires authentication.
    /// </summary>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of records per page (default: 20, max: 100).</param>
    /// <param name="ct">Cancellation token.</param>
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
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="page">1-based page number (default: 1).</param>
    /// <param name="pageSize">Number of records per page (default: 20, max: 100).</param>
    /// <param name="ct">Cancellation token.</param>
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
