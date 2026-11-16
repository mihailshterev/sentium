using System.Globalization;
using System.Net;
using Sentium.Core.Constants;
using Sentium.Praxis.Contracts;
using Sentium.Sentinel.Contracts;

namespace Sentium.Praxis.Features.CodeExecution;

/// <summary>
/// Typed HTTP client for the Sentium.Sandbox service. Every run is authorized by the Sentinel PDP in-process
/// first (fail-closed) - the only Sentinel check of an agent's code run, see
/// <see cref="AgentToolPolicyAttribute.AuthorizedByCallee"/>; only an allowed run reaches the Sandbox, which trusts its caller through the internal API
/// key alone. The sandbox is stateless, so the execution history lives here: each completed or denied call is
/// written as an <see cref="ExecutionLogEntry"/>. The write uses its own DI scope so it never shares a DbContext
/// with the agent loop that triggered it.
/// </summary>
internal sealed class SandboxClient(
    HttpClient httpClient,
    IPolicyDecisionPoint sentinel,
    IServiceScopeFactory scopeFactory,
    ILogger<SandboxClient> logger) : ISandboxClient
{
    private const string SkillName = "sandbox.execute";
    private const string DefaultPrompt = "sandbox code execution";

    /// <summary>
    /// Upper bound on the code forwarded to the PDP for content inspection. Mirrors the tool-payload window so
    /// the sensitive-data egress scan can inspect what is actually being executed without shipping arbitrarily
    /// large source (which would also risk tripping the scan's regex timeout).
    /// </summary>
    private const int MaxScanChars = 8 * 1024;

    public async Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, string correlationId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<ExecutionLanguage>(request.Language, ignoreCase: true, out var language) || !Enum.IsDefined(language))
        {
            throw new SandboxRejectedException(HttpStatusCode.BadRequest, $"Unsupported language '{request.Language}'.");
        }

        var decision = await sentinel.EvaluateAsync(ToPolicyRequest(request, language, correlationId), ct);

        var result = decision.Allowed
            ? await RunAsync(request, correlationId, decision.AuditId, ct)
            : Denied(request, language, decision);

        await RecordAsync(request, language, correlationId, result, ct);

        return result;
    }

    private static PolicyEvaluationRequest ToPolicyRequest(SandboxExecutionRequest request, ExecutionLanguage language, string correlationId)
    {
        return new PolicyEvaluationRequest
        {
            AgentId = request.AgentId,
            SkillName = SkillName,
            ResourceType = "Code",
            ResourceId = language.ToString(),
            Action = "execute",
            OriginalUserPrompt = request.OriginalUserPrompt ?? DefaultPrompt,
            CorrelationId = correlationId,
            UserId = request.UserId,
            Metadata = new Dictionary<string, string>
            {
                ["language"] = language.ToString(),
                ["codeLength"] = request.Code.Length.ToString(CultureInfo.InvariantCulture),
                ["fileContextCount"] = (request.FileContext?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
                // The (capped) source, so content-inspection layers (sensitive-data egress) scan what is executed.
                [PolicyEvaluationRequest.PayloadKey] = request.Code.Length > MaxScanChars ? request.Code[..MaxScanChars] : request.Code
            }
        };
    }

    private async Task<SandboxExecutionResponse> RunAsync(SandboxExecutionRequest request, string correlationId, Guid auditId, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "execute")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(CommonHeaderNames.CorrelationId, correlationId);

        using var response = await httpClient.SendAsync(message, ct);

        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(ct);
            throw new SandboxRejectedException(response.StatusCode, details);
        }

        var run = await response.Content.ReadFromJsonAsync<SandboxRunResponse>(ct)
            ?? throw new InvalidOperationException("Sandbox returned an empty execution result.");

        return new SandboxExecutionResponse
        {
            Succeeded = run.Succeeded,
            ExitCode = run.ExitCode,
            Output = run.Output,
            Error = run.Error,
            TimedOut = run.TimedOut,
            JobId = run.JobId,
            SentinelAuditId = auditId,
            DurationMs = run.DurationMs,
            Artifacts = run.Artifacts
        };
    }

    private SandboxExecutionResponse Denied(SandboxExecutionRequest request, ExecutionLanguage language, PolicyEvaluationResponse decision)
    {
        // A fail-closed deny has no audit row; the history entry still needs an id to link to.
        var auditId = decision.AuditId == Guid.Empty ? Guid.NewGuid() : decision.AuditId;

        logger.LogWarning(
            "Sentinel PDP denied sandbox execution. AgentId={AgentId} Language={Language} AuditId={AuditId} Reason={Reason}",
            request.AgentId, language, auditId, decision.Reason);

        return new SandboxExecutionResponse
        {
            Succeeded = false,
            ExitCode = -1,
            Output = string.Empty,
            Error = string.Empty,
            TimedOut = false,
            PolicyDenied = true,
            PolicyDenialReason = decision.Reason,
            JobId = Guid.NewGuid(),
            SentinelAuditId = auditId,
            DurationMs = 0
        };
    }

    private async Task RecordAsync(SandboxExecutionRequest request, ExecutionLanguage language, string correlationId, SandboxExecutionResponse result, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var executionLog = scope.ServiceProvider.GetRequiredService<IExecutionLogRepository>();

            await executionLog.AddAsync(new ExecutionLogEntry
            {
                JobId = result.JobId,
                ExecutedAt = DateTimeOffset.UtcNow,
                AgentId = request.AgentId,
                UserId = request.UserId,
                CorrelationId = correlationId,
                Language = language,
                Code = request.Code,
                OriginalUserPrompt = request.OriginalUserPrompt,
                FileContext = [.. (request.FileContext ?? []).Select(f => new SandboxFileContext { FileName = f.FileName, Content = f.Content })],
                Succeeded = result.Succeeded,
                ExitCode = result.ExitCode,
                Output = result.Output,
                Error = result.Error,
                TimedOut = result.TimedOut,
                PolicyDenied = result.PolicyDenied,
                PolicyDenialReason = result.PolicyDenialReason,
                SentinelAuditId = result.SentinelAuditId,
                DurationMs = result.DurationMs,
                Artifacts = [.. result.Artifacts.Select(a => new ArtifactRecord
                {
                    FileName = a.FileName,
                    MimeType = a.MimeType,
                    BlobUri = new Uri(a.BlobUri),
                    SizeBytes = a.SizeBytes
                })]
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The code already ran (or was denied); losing the history row must not turn the result into a failure.
            logger.LogError(ex, "Failed to record sandbox execution {JobId} for agent '{AgentId}'", result.JobId, request.AgentId);
        }
    }
}
