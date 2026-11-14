using Sentium.Core.Constants;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.CodeExecution;

/// <summary>
/// Typed HTTP client for the Sentium.Sandbox service. The sandbox is stateless, so the execution
/// history lives here: each completed call is written as an <see cref="ExecutionLogEntry"/>.
/// The write uses its own DI scope so it never shares a DbContext with the agent loop that triggered it.
/// </summary>
internal sealed class SandboxClient(
    HttpClient httpClient,
    IServiceScopeFactory scopeFactory,
    ILogger<SandboxClient> logger) : ISandboxClient
{
    public async Task<SandboxExecutionResponse> ExecuteAsync(SandboxExecutionRequest request, string correlationId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        var result = await response.Content.ReadFromJsonAsync<SandboxExecutionResponse>(ct)
            ?? throw new InvalidOperationException("Sandbox returned an empty execution result.");

        await RecordAsync(request, correlationId, result, ct);

        return result;
    }

    private async Task RecordAsync(SandboxExecutionRequest request, string correlationId, SandboxExecutionResponse result, CancellationToken ct)
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
                CorrelationId = correlationId,
                Language = Enum.Parse<ExecutionLanguage>(request.Language, ignoreCase: true),
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
            // The code already ran; losing the history row must not turn a finished run into a failure.
            logger.LogError(ex, "Failed to record sandbox execution {JobId} for agent '{AgentId}'", result.JobId, request.AgentId);
        }
    }
}
