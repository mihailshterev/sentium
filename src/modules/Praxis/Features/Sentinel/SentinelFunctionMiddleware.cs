using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Tools;

namespace Sentium.Praxis.Features.Sentinel;

/// <summary>
/// Agent Framework function-calling middleware that puts every Sentium tool call through the Sentinel policy decision
/// point. A denied call never reaches the tool: the model receives the denial reason as the function result. If Sentinel
/// is unreachable, <see cref="SentinelClient"/> fails closed (deny).
/// </summary>
/// <remarks>
/// Runs after human approval (inside function invocation), so approved calls are still policy-checked. Functions that are
/// not Sentium tools - the framework's skill functions - carry no Sentinel classification and pass straight through.
/// </remarks>
public sealed class SentinelFunctionMiddleware(SentinelClient sentinel, IPdpContextAccessor runContext, ILogger<SentinelFunctionMiddleware> logger)
{
    private const int MaxPayloadChars = 8 * 1024;
    private const int MaxResourceIdChars = 200;

    private static readonly string[] ResourceIdKeys = ["input", "path", "query", "workspace", "workspaceId", "fileId", "fileName"];
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata = new Dictionary<string, string>();

    public async ValueTask<object?> InvokeAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (!AgentToolFunction.TryGetClassification(context.Function, out var resource, out var action))
        {
            return await next(context, cancellationToken);
        }

        var agentName = string.IsNullOrWhiteSpace(agent.Name) ? "Agent" : agent.Name;

        var decision = await sentinel.EvaluateAsync(new PdpRequest
        {
            AgentId = agentName,
            SkillName = context.Function.Name,
            ResourceType = resource,
            ResourceId = ExtractResourceId(context.Arguments),
            Action = action,
            OriginalUserPrompt = runContext.OriginalUserPrompt,
            CorrelationId = runContext.CorrelationId.Length > 0 ? runContext.CorrelationId : Guid.NewGuid().ToString(),
            Metadata = BuildMetadata(action, context.Arguments),
        }, cancellationToken);

        if (!decision.Allowed)
        {
            logger.LogWarning("[PDP DENY] Agent={Agent} Tool={Tool} Risk={Risk} AuditId={AuditId} Reason={Reason}", agentName, context.Function.Name, decision.Risk, decision.AuditId, decision.Reason);

            return $"[Access Denied by Security Policy] {decision.Reason}";
        }

        return await next(context, cancellationToken);
    }

    /// <summary>
    /// For write/execute actions, forwards the (capped) argument payload so Sentinel's content-inspection layers
    /// (sensitive-data egress, semantic intent) can scan what is actually written or executed. Reads and searches carry
    /// no payload to keep evaluation cheap and avoid leaking query text.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildMetadata(string action, AIFunctionArguments arguments)
    {
        var isEgress = action is nameof(ToolAction.Write) or nameof(ToolAction.Execute);
        if (!isEgress || arguments.Count == 0)
        {
            return EmptyMetadata;
        }

        var payload = string.Join(" ", arguments.Values.Select(v => v?.ToString()).Where(s => !string.IsNullOrEmpty(s)));
        if (payload.Length == 0)
        {
            return EmptyMetadata;
        }

        return new Dictionary<string, string> { ["payload"] = payload.Length > MaxPayloadChars ? payload[..MaxPayloadChars] : payload };
    }

    private static string ExtractResourceId(AIFunctionArguments arguments)
    {
        foreach (var key in ResourceIdKeys)
        {
            if (arguments.TryGetValue(key, out var value))
            {
                var text = value?.ToString() ?? string.Empty;
                return text.Length > MaxResourceIdChars ? text[..MaxResourceIdChars] : text;
            }
        }

        return "*";
    }
}
