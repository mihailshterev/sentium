using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Sentium.AgentRuntime.Infrastructure.Sentinel;

/// <summary>
/// Decorates an <see cref="AIFunction"/> with a Sentinel PDP check.
/// The tool is only executed if the policy engine returns <c>Allowed = true</c>.
/// If Sentinel is unreachable the decorator fails closed (denies the call).
/// </summary>
public sealed class SentinelGuardedAIFunction(
    AIFunction inner,
    SentinelClient sentinel,
    IPdpContextAccessor pdpContext,
    string agentName,
    ILogger logger) : AIFunction
{
    public override string Name => inner.Name;
    public override string Description => inner.Description;
    public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;

    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        pdpContext.AgentName = agentName;

        var request = new PdpRequest
        {
            AgentId = agentName,
            SkillName = inner.Name,
            ResourceType = ResolveResourceType(inner.Name),
            ResourceId = ExtractResourceId(arguments),
            Action = ResolveAction(inner.Name),
            OriginalUserPrompt = pdpContext.OriginalUserPrompt,
            CorrelationId = pdpContext.CorrelationId.Length > 0 ? pdpContext.CorrelationId : Guid.NewGuid().ToString()
        };

        var decision = await sentinel.EvaluateAsync(request, cancellationToken);

        if (!decision.Allowed)
        {
            logger.LogWarning("[PDP DENY] Agent={Agent} Tool={Tool} Risk={Risk} AuditId={AuditId} Reason={Reason}", agentName, inner.Name, decision.Risk, decision.AuditId, decision.Reason);

            return $"[Access Denied by Security Policy] {decision.Reason}";
        }

        return await inner.InvokeAsync(arguments, cancellationToken);
    }

    private static readonly FrozenDictionary<string, string> ResourceTypeMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["knowledge_base_search"] = "VectorDb",
            ["recall_memory"] = "VectorDb",
            ["store_memory"] = "Memory",
            ["capture_agent_learning"] = "Memory",
            ["read_file"] = "File",
            ["list_workspaces"] = "File",
            ["list_workspace_files"] = "File",
            ["read_workspace_file"] = "File",
            ["read_workspace_file_content"] = "File",
            ["write_workspace_file"] = "File",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> ActionMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["knowledge_base_search"] = "Search",
            ["recall_memory"] = "Search",
            ["store_memory"] = "Write",
            ["capture_agent_learning"] = "Write",
            ["read_file"] = "Read",
            ["list_workspaces"] = "Read",
            ["list_workspace_files"] = "Read",
            ["read_workspace_file"] = "Read",
            ["read_workspace_file_content"] = "Read",
            ["write_workspace_file"] = "Write",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static string ResolveResourceType(string toolName) => ResourceTypeMap.TryGetValue(toolName, out var t) ? t : "Any";

    private static string ResolveAction(string toolName) => ActionMap.TryGetValue(toolName, out var a) ? a : "Execute";

    private static readonly string[] ResourceIdKeys = ["input", "path", "query", "workspaceId", "fileName"];

    private static string ExtractResourceId(AIFunctionArguments arguments)
    {
        foreach (var key in ResourceIdKeys)
        {
            if (arguments.TryGetValue(key, out var value))
            {
                var str = value?.ToString() ?? string.Empty;
                return str.Length > 200 ? str[..200] : str;
            }
        }

        return "*";
    }
}
