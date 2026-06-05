using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.Settings;

namespace Sentium.Sentinel.Infrastructure.Policies;

/// <summary>
/// Validates the agent's declared action against the original user prompt using a local LLM.
/// <para/>
/// This policy detects prompt injections and semantic intent mismatches, preventing
/// a compromised or hallucinating agent from executing actions unrelated to the user's request.
/// </summary>
public sealed class SemanticIntentPolicy(
    IChatClient chatClient,
    IPdpRuntimeSettingsProvider settings,
    IOptions<PdpOptions> opts,
    ILogger<SemanticIntentPolicy> logger) : IPdpPolicy
{
    private readonly PdpOptions _options = opts.Value;

    public string Name => "SemanticIntentCheck";

    public async Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtime = await settings.GetAsync(ct);

        if (!runtime.SemanticIntentCheckEnabled)
        {
            return null;
        }

        if (runtime.AutonomyLevel >= 9)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.OriginalUserPrompt) || request.OriginalUserPrompt.Length < 5)
        {
            return null;
        }

        var verdict = await ClassifyIntentAsync(request, runtime, ct);
        var verdictLabel = verdict switch
        {
            IntentVerdict.Aligned => "Aligned",
            IntentVerdict.Misaligned => "Misaligned",
            _ => "Inconclusive"
        };

        var effectiveVerdict = verdict;
        if (verdict == IntentVerdict.Inconclusive && runtime.AutonomyLevel <= 2)
        {
            effectiveVerdict = IntentVerdict.Misaligned;
        }

        if (effectiveVerdict == IntentVerdict.Misaligned)
        {
            logger.LogWarning("Semantic intent mismatch detected. Agent={AgentId} Skill={Skill} Action={Action} Resource={ResourceType}", request.AgentId, request.SkillName, request.Action, request.ResourceType);

            return new PolicyDecision
            {
                Allowed = false,
                Effect = PolicyEffect.DenyWithAlert,
                Reason = $"Semantic intent check failed: the requested action " +
                         $"(skill='{request.SkillName}', action='{request.Action}', resource='{request.ResourceType}') " +
                         "does not align with the original user prompt. " +
                         "This may indicate prompt injection or agent hallucination.",
                Risk = PolicyRiskLevel.High,
                AuditId = Guid.Empty,
                TriggeredPolicies = [Name],
                AlignmentVerdict = verdictLabel
            };
        }

        return new PolicyDecision
        {
            Allowed = true,
            Effect = PolicyEffect.Allow,
            Reason = "Semantic intent check passed.",
            Risk = PolicyRiskLevel.Low,
            AuditId = Guid.Empty,
            TriggeredPolicies = [Name],
            AlignmentVerdict = verdictLabel
        };
    }

    private async Task<IntentVerdict> ClassifyIntentAsync(PolicyRequest request, PdpRuntimeSettings runtime, CancellationToken ct)
    {
        var prompt = BuildClassificationPrompt(request);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(_options.IntentCheckTimeoutSeconds));

        try
        {
            var chatOptions = new ChatOptions
            {
                Temperature = 0f,
                ModelId = string.IsNullOrWhiteSpace(runtime.IntentCheckModel) ? null : runtime.IntentCheckModel
            };

            var message = new ChatMessage(ChatRole.User, prompt);

            var response = await chatClient.GetResponseAsync([message], chatOptions, linkedCts.Token);

            var span = (response.Text ?? string.Empty).AsSpan().Trim();

            if (span.StartsWith("MISALIGNED", StringComparison.OrdinalIgnoreCase))
            {
                return IntentVerdict.Misaligned;
            }

            if (span.StartsWith("ALIGNED", StringComparison.OrdinalIgnoreCase))
            {
                return IntentVerdict.Aligned;
            }

            logger.LogWarning("Semantic intent classifier returned unexpected output: '{Output}'. Treating as inconclusive.", span.Length > 80 ? span[..80].ToString() : span.ToString());

            return IntentVerdict.Inconclusive;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Semantic intent check timed out after {TimeoutSeconds}s. Treating as inconclusive.", _options.IntentCheckTimeoutSeconds);

            return IntentVerdict.Inconclusive;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Semantic intent check failed due to an exception. Allowing request to avoid availability impact.");

            return IntentVerdict.Inconclusive;
        }
    }

    private static string BuildClassificationPrompt(PolicyRequest request) => $"""
        You are a security intent validator for an AI platform.
        Your only job is to decide whether an agent's action aligns with the user's stated goal.

        User's original request: "{request.OriginalUserPrompt}"

        Agent is attempting to:
        - Skill / tool: {request.SkillName}
        - Action: {request.Action}
        - Resource type: {request.ResourceType}
        - Resource: {request.ResourceId}

        Rules:
        - Answer ALIGNED if the agent action is a reasonable step to satisfy the user request.
        - Answer MISALIGNED if the action is unrelated, suspicious, or clearly inconsistent with the user's intent.
        - Answer INCONCLUSIVE if you cannot determine alignment.

        Respond with exactly one word: ALIGNED, MISALIGNED, or INCONCLUSIVE.
        """;

    private enum IntentVerdict { Aligned, Misaligned, Inconclusive }
}
