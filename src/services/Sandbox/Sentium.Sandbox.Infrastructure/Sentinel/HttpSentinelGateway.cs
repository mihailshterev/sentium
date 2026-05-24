using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Sentium.Sandbox.Application.Sentinel;
using Sentium.Sandbox.Core.Models;

namespace Sentium.Sandbox.Infrastructure.Sentinel;

/// <summary>
/// Typed HTTP client that submits authorization requests to the Sentinel PDP.
/// Fail-closed: any communication failure results in a denial.
/// </summary>
internal sealed class HttpSentinelGateway(HttpClient httpClient, ILogger<HttpSentinelGateway> logger) : ISentinelGateway
{
    /// <inheritdoc />
    public async Task<SentinelAuthorizationResult> AuthorizeExecutionAsync(ExecutionRequest request, CancellationToken ct)
    {
        var body = new SentinelEvaluationRequest
        {
            AgentId = request.AgentId,
            SkillName = "sandbox.execute",
            ResourceType = "Code",
            ResourceId = request.Language.ToString(),
            Action = "execute",
            OriginalUserPrompt = request.OriginalUserPrompt ?? "sandbox code execution",
            CorrelationId = request.CorrelationId,
            Metadata = new Dictionary<string, string>
            {
                ["language"] = request.Language.ToString(),
                ["codeLength"] = request.Code.Length.ToString(),
                ["fileContextCount"] = request.FileContext.Count.ToString()
            }
        };

        try
        {
            var response = await httpClient.PostAsJsonAsync("policy/evaluate", body, ct);
            response.EnsureSuccessStatusCode();

            var decision = await response.Content.ReadFromJsonAsync<SentinelDecisionResponse>(ct);

            if (decision is null)
            {
                logger.LogError("Sentinel PDP returned an empty response for agent '{AgentId}'.", request.AgentId);
                return SentinelAuthorizationResult.Deny("Sentinel PDP returned an empty response. Failing closed.");
            }

            return decision.Allowed ? SentinelAuthorizationResult.Allow(decision.AuditId) : SentinelAuthorizationResult.Deny(decision.Reason, decision.AuditId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not reach Sentinel PDP for agent '{AgentId}' (Language={Language}). Failing closed.", request.AgentId, request.Language);

            return SentinelAuthorizationResult.Deny("Sentinel PDP is unavailable. Execution denied for safety.");
        }
    }

    private sealed record SentinelEvaluationRequest
    {
        public required string AgentId { get; init; }
        public required string SkillName { get; init; }
        public required string ResourceType { get; init; }
        public required string ResourceId { get; init; }
        public required string Action { get; init; }
        public required string OriginalUserPrompt { get; init; }
        public required string CorrelationId { get; init; }
        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    }

    private sealed record SentinelDecisionResponse
    {
        public bool Allowed { get; init; }
        public string Effect { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public string Risk { get; init; } = string.Empty;
        public Guid AuditId { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public IReadOnlyList<string> TriggeredPolicies { get; init; } = [];
    }
}
