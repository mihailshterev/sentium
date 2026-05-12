using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Engine;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Dtos;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Api.Controllers;

/// <summary>
/// Policy Decision Point (PDP) API.
/// <para>
/// <c>POST /policy/evaluate</c> is intentionally <see cref="AllowAnonymousAttribute"/> because it
/// receives direct service-to-service calls from AgentRuntime (and future Sandbox) over the
/// internal Aspire network — those callers have no user JWT to forward.
/// The audit endpoints remain protected for human operators only.
/// </para>
/// </summary>
[ApiController]
[Route("policy")]
public sealed class PolicyController(
    SentinelPolicyEngine engine,
    IAuditLog auditLog,
    IOptionsMonitor<PdpOptions> optionsMonitor) : ControllerBase
{
    /// <summary>
    /// Evaluates a policy request and returns an authorization decision.
    /// Open to internal service callers — protected at the network layer by Aspire.
    /// </summary>
    [HttpPost("evaluate")]
    [AllowAnonymous]
    [ProducesResponseType<PolicyEvaluationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EvaluateAsync([FromBody] PolicyEvaluationRequest body, CancellationToken ct)
    {
        if (!Enum.TryParse<ResourceType>(body.ResourceType, ignoreCase: true, out var resourceType))
        {
            return BadRequest($"Unknown resource type '{body.ResourceType}'. " + $"Valid values: {string.Join(", ", Enum.GetNames<ResourceType>())}");
        }

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
    [Authorize]
    [ProducesResponseType<IReadOnlyList<AuditRecord>>(StatusCodes.Status200OK)]
    public IActionResult GetAudit([FromQuery] int count = 100)
    {
        var records = auditLog.GetRecent(Math.Min(count, 500));
        return Ok(records);
    }

    /// <summary>
    /// Returns audit records for a specific agent. Requires authentication.
    /// </summary>
    [HttpGet("audit/agent/{agentId}")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<AuditRecord>>(StatusCodes.Status200OK)]
    public IActionResult GetAuditByAgent(string agentId, [FromQuery] int count = 50)
    {
        var records = auditLog.GetByAgent(agentId, Math.Min(count, 200));
        return Ok(records);
    }

    /// <summary>
    /// Returns aggregate statistics for the current audit window. Requires authentication.
    /// </summary>
    [HttpGet("audit/stats")]
    [Authorize]
    [ProducesResponseType<AuditStatsDto>(StatusCodes.Status200OK)]
    public IActionResult GetAuditStats()
    {
        var records = auditLog.GetRecent(500);

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
    /// Returns the current runtime-configurable PDP settings. Requires authentication.
    /// </summary>
    [HttpGet("settings")]
    [Authorize]
    [ProducesResponseType<PdpSettingsDto>(StatusCodes.Status200OK)]
    public IActionResult GetSettings()
    {
        var opts = optionsMonitor.CurrentValue;
        return Ok(new PdpSettingsDto
        {
            LockdownMode = opts.LockdownMode,
            AutonomyLevel = opts.AutonomyLevel,
            SemanticIntentCheckEnabled = opts.SemanticIntentCheckEnabled,
            RateLimitMaxRequests = opts.RateLimitMaxRequests,
            RateLimitWindowSeconds = opts.RateLimitWindowSeconds
        });
    }

    /// <summary>
    /// Updates runtime-configurable PDP settings. Requires authentication.
    /// Changes take effect immediately without restarting the service.
    /// </summary>
    [HttpPut("settings")]
    [Authorize]
    [ProducesResponseType<PdpSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult UpdateSettings([FromBody] UpdatePdpSettingsRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var opts = optionsMonitor.CurrentValue;

        if (body.LockdownMode.HasValue)
        {
            opts.LockdownMode = body.LockdownMode.Value;
        }

        if (body.AutonomyLevel.HasValue)
        {
            if (body.AutonomyLevel.Value is < 1 or > 10)
            {
                return BadRequest("AutonomyLevel must be between 1 and 10.");
            }

            opts.AutonomyLevel = body.AutonomyLevel.Value;
        }

        if (body.SemanticIntentCheckEnabled.HasValue)
        {
            opts.SemanticIntentCheckEnabled = body.SemanticIntentCheckEnabled.Value;
        }

        if (body.RateLimitMaxRequests.HasValue)
        {
            if (body.RateLimitMaxRequests.Value < 1)
            {
                return BadRequest("RateLimitMaxRequests must be at least 1.");
            }

            opts.RateLimitMaxRequests = body.RateLimitMaxRequests.Value;
        }

        if (body.RateLimitWindowSeconds.HasValue)
        {
            if (body.RateLimitWindowSeconds.Value < 1)
            {
                return BadRequest("RateLimitWindowSeconds must be at least 1.");
            }

            opts.RateLimitWindowSeconds = body.RateLimitWindowSeconds.Value;
        }

        return Ok(new PdpSettingsDto
        {
            LockdownMode = opts.LockdownMode,
            AutonomyLevel = opts.AutonomyLevel,
            SemanticIntentCheckEnabled = opts.SemanticIntentCheckEnabled,
            RateLimitMaxRequests = opts.RateLimitMaxRequests,
            RateLimitWindowSeconds = opts.RateLimitWindowSeconds
        });
    }
}
