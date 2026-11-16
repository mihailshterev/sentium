using Microsoft.AspNetCore.Mvc;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Sentinel.Features.Audit;

namespace Sentium.Sentinel.Features.Engine;

/// <summary>
/// The Sentinel audit trail. Policy evaluation has no HTTP endpoint: every caller lives in Sentium.Web and
/// evaluates in-process through <see cref="Sentium.Sentinel.Contracts.IPolicyDecisionPoint"/>.
/// </summary>
[ApiController]
[Route($"{SentinelRoutes.Prefix}/policy")]
internal sealed class PolicyController(IAuditLog auditLog) : ControllerBase
{
    private const int StatsWindow = 500;

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
        var stats = await auditLog.GetStatsAsync(StatsWindow, ct);

        return Ok(stats);
    }
}
