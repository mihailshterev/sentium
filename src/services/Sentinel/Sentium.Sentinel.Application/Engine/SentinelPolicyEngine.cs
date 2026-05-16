using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Sentium.Sentinel.Application.Audit;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Application.Engine;

/// <summary>
/// The Policy Decision Point (PDP) engine.
/// Orchestrates the Defence-in-Depth policy stack, writes forensic audit records,
/// and enforces a fail-closed posture: any unhandled exception results in a deny.
/// </summary>
public sealed class SentinelPolicyEngine(
    IEnumerable<IPdpPolicy> policies,
    IAuditLog auditLog,
    ILogger<SentinelPolicyEngine> logger)
{
    private readonly IReadOnlyList<IPdpPolicy> _policies = policies.ToList();

    /// <summary>
    /// Evaluates a <see cref="PolicyRequest"/> against all registered policy layers
    /// and returns a <see cref="PolicyDecision"/>.
    /// </summary>
    public async Task<PolicyDecision> EvaluateAsync(PolicyRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var auditId = Guid.NewGuid();
        var sw = Stopwatch.StartNew();
        var triggeredPolicies = new List<string>();
        PolicyDecision? decision = null;
        string? alignmentVerdict = null;

        try
        {
            foreach (var policy in _policies)
            {
                PolicyDecision? result = null;

                try
                {
                    result = await policy.EvaluateAsync(request, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Policy '{PolicyName}' threw an unhandled exception for agent '{AgentId}'. Failing closed.", policy.Name, request.AgentId);

                    result = PolicyDecision.Deny(
                        $"Policy layer '{policy.Name}' encountered an internal error. Request denied for safety.",
                        auditId,
                        [policy.Name],
                        PolicyRiskLevel.Critical,
                        alert: true);
                }

                if (result is not null)
                {
                    triggeredPolicies.Add(policy.Name);

                    if (result.AlignmentVerdict is not null)
                    {
                        alignmentVerdict = result.AlignmentVerdict;
                    }

                    if (!result.Allowed)
                    {
                        decision = result with { AuditId = auditId, TriggeredPolicies = triggeredPolicies };
                        break;
                    }
                }
            }

            decision ??= PolicyDecision.Allow(auditId, triggeredPolicies) with { AlignmentVerdict = alignmentVerdict };
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "PDP engine itself encountered a fatal error for agent '{AgentId}'. Failing closed.", request.AgentId);

            decision = PolicyDecision.Deny(
                "PDP engine encountered a critical error. Request denied.",
                auditId,
                triggeredPolicies,
                PolicyRiskLevel.Critical,
                alert: true);
        }
        finally
        {
            sw.Stop();
            await WriteAuditAsync(request, decision!, auditId, sw.ElapsedMilliseconds, ct);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "PDP [{AuditId}] Agent={AgentId} Skill={Skill} Action={Action} Resource={ResourceType}/{ResourceId} → {Effect} ({Risk}) [{Duration}ms]",
                auditId, request.AgentId, request.SkillName, request.Action,
                request.ResourceType, request.ResourceId,
                decision!.Effect, decision.Risk, sw.ElapsedMilliseconds);
        }

        return decision!;
    }

    private async Task WriteAuditAsync(
        PolicyRequest request,
        PolicyDecision decision,
        Guid auditId,
        long durationMs,
        CancellationToken ct)
    {
        var record = new AuditRecord
        {
            Id = auditId,
            Timestamp = DateTimeOffset.UtcNow,
            AgentId = request.AgentId,
            SkillName = request.SkillName,
            ResourceType = request.ResourceType,
            ResourceId = request.ResourceId,
            Action = request.Action,
            UserPromptHash = InMemoryAuditLog.HashPrompt(request.OriginalUserPrompt),
            CorrelationId = request.CorrelationId,
            Metadata = request.Metadata,
            Allowed = decision.Allowed,
            Effect = decision.Effect,
            Reason = decision.Reason,
            Risk = decision.Risk,
            TriggeredPolicies = decision.TriggeredPolicies,
            EvaluationDurationMs = durationMs,
            AlignmentVerdict = decision.AlignmentVerdict
        };

        await auditLog.RecordAsync(record, ct);
    }
}
