using Sentium.Sandbox.Models;
using Sentium.Sandbox.Sentinel;

namespace Sentium.Sandbox.Execution;

/// <summary>
/// Coordinates the two-phase sandbox workflow:
/// <list type="number">
///   <item><description>Authorize the execution with the Sentinel PDP (fail-closed).</description></item>
///   <item><description>Delegate to <see cref="ISandboxRunner"/> to spawn the container.</description></item>
/// </list>
/// The sandbox is stateless: the caller (Praxis) records the outcome in its execution history.
/// </summary>
internal sealed class SandboxOrchestrator(
    ISandboxRunner runner,
    ISentinelGateway sentinelGateway,
    ILogger<SandboxOrchestrator> logger)
{
    /// <summary>
    /// Runs the submitted code after obtaining a PDP permit.
    /// Returns a policy-denied <see cref="ExecutionResult"/> immediately if Sentinel refuses.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authorization = await sentinelGateway.AuthorizeExecutionAsync(request, ct);

        if (!authorization.IsAllowed)
        {
            var auditId = authorization.AuditId == Guid.Empty ? Guid.NewGuid() : authorization.AuditId;

            logger.LogWarning("Sentinel PDP denied sandbox execution. AgentId={AgentId} Language={Language} AuditId={AuditId} Reason={Reason}", request.AgentId, request.Language, auditId, authorization.Reason);

            return new ExecutionResult
            {
                Succeeded = false,
                ExitCode = -1,
                Output = string.Empty,
                Error = string.Empty,
                TimedOut = false,
                PolicyDenied = true,
                PolicyDenialReason = authorization.Reason,
                SentinelAuditId = auditId,
                JobId = Guid.NewGuid(),
                DurationMs = 0
            };
        }

        logger.LogInformation("Sentinel PDP authorized sandbox execution. AgentId={AgentId} Language={Language} AuditId={AuditId}", request.AgentId, request.Language, authorization.AuditId);

        var result = await runner.RunAsync(request, ct);
        return result with { SentinelAuditId = authorization.AuditId };
    }
}
