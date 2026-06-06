using Microsoft.Extensions.Logging;
using Sentium.Sandbox.Application.Sentinel;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Application;

/// <summary>
/// Coordinates the two-phase sandbox workflow:
/// <list type="number">
///   <item><description>Authorize the execution with the Sentinel PDP (fail-closed).</description></item>
///   <item><description>Delegate to <see cref="ISandboxRunner"/> to spawn the container.</description></item>
///   <item><description>Persist the outcome to <see cref="IExecutionLogRepository"/>.</description></item>
/// </list>
/// </summary>
public sealed class SandboxOrchestrator(
    ISandboxRunner runner,
    ISentinelGateway sentinelGateway,
    IExecutionLogRepository executionLog,
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

            var deniedResult = new ExecutionResult
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

            await executionLog.AddAsync(BuildLogEntry(request, deniedResult), ct);
            return deniedResult;
        }

        logger.LogInformation("Sentinel PDP authorized sandbox execution. AgentId={AgentId} Language={Language} AuditId={AuditId}", request.AgentId, request.Language, authorization.AuditId);

        var result = await runner.RunAsync(request, ct);
        var finalResult = result with { SentinelAuditId = authorization.AuditId };

        await executionLog.AddAsync(BuildLogEntry(request, finalResult), ct);
        return finalResult;
    }

    private static ExecutionLogEntry BuildLogEntry(ExecutionRequest request, ExecutionResult result) => new()
    {
        JobId = result.JobId,
        ExecutedAt = DateTimeOffset.UtcNow,
        AgentId = request.AgentId,
        CorrelationId = request.CorrelationId,
        Language = request.Language,
        Code = request.Code,
        OriginalUserPrompt = request.OriginalUserPrompt,
        FileContext = [.. request.FileContext],
        Succeeded = result.Succeeded,
        ExitCode = result.ExitCode,
        Output = result.Output,
        Error = result.Error,
        TimedOut = result.TimedOut,
        PolicyDenied = result.PolicyDenied,
        PolicyDenialReason = result.PolicyDenialReason,
        SentinelAuditId = result.SentinelAuditId,
        DurationMs = result.DurationMs,
        Artifacts = [.. result.Artifacts]
    };
}
