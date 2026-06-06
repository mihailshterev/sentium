using Microsoft.AspNetCore.Mvc;
using Sentium.Infrastructure.Security;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Dtos;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.Settings;

namespace Sentium.Sentinel.Api.Controllers;

/// <summary>
/// Policy Decision Point (PDP) API.
/// </summary>
[ApiController]
[Route("policy")]
public sealed class PolicyController(
    SentinelPolicyEngine engine,
    IAuditLog auditLog,
    IPdpRuntimeSettingsProvider pdpSettings) : ControllerBase
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
    /// Returns recent forensic audit records, newest first. Requires authentication.
    /// </summary>
    [HttpGet("audit")]
    [AuthorizeSovereign]
    [ProducesResponseType<IReadOnlyList<AuditRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAudit([FromQuery] int count = 100, CancellationToken ct = default)
    {
        var records = await auditLog.GetRecentAsync(Math.Min(count, 500), ct);
        return Ok(records);
    }

    /// <summary>
    /// Returns audit records for a specific agent. Requires authentication.
    /// </summary>
    [HttpGet("audit/agent/{agentId}")]
    [AuthorizeSovereign]
    [ProducesResponseType<IReadOnlyList<AuditRecord>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditByAgent(string agentId, [FromQuery] int count = 50, CancellationToken ct = default)
    {
        var records = await auditLog.GetByAgentAsync(agentId, Math.Min(count, 200), ct);
        return Ok(records);
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

    /// <summary>
    /// Returns the current runtime-configurable PDP settings.
    /// </summary>
    [HttpGet("settings")]
    [AuthorizeSovereign]
    [ProducesResponseType<PdpSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var runtime = await pdpSettings.GetAsync(ct);
        return Ok(ToDto(runtime));
    }

    /// <summary>
    /// Updates runtime-configurable PDP settings.
    /// </summary>
    [HttpPut("settings")]
    [AuthorizeSovereign]
    [ProducesResponseType<PdpSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdatePdpSettingsRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var current = await pdpSettings.GetAsync(ct);
        var merged = current with
        {
            LockdownMode = body.LockdownMode ?? current.LockdownMode,
            AutonomyLevel = body.AutonomyLevel ?? current.AutonomyLevel,
            SemanticIntentCheckEnabled = body.SemanticIntentCheckEnabled ?? current.SemanticIntentCheckEnabled,
            IntentCheckModel = body.IntentCheckModel ?? current.IntentCheckModel,
            RateLimitMaxRequests = body.RateLimitMaxRequests ?? current.RateLimitMaxRequests,
            RateLimitWindowSeconds = body.RateLimitWindowSeconds ?? current.RateLimitWindowSeconds,
        };

        await pdpSettings.UpdateAsync(merged, ct);

        return Ok(ToDto(merged));
    }

    private static PdpSettingsDto ToDto(PdpRuntimeSettings runtime) => new()
    {
        LockdownMode = runtime.LockdownMode,
        AutonomyLevel = runtime.AutonomyLevel,
        SemanticIntentCheckEnabled = runtime.SemanticIntentCheckEnabled,
        IntentCheckModel = runtime.IntentCheckModel,
        RateLimitMaxRequests = runtime.RateLimitMaxRequests,
        RateLimitWindowSeconds = runtime.RateLimitWindowSeconds
    };
}
