using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.AgentRuntime.Core.Tools;

namespace Sentium.AgentRuntime.Infrastructure.Tools;

/// <summary>
/// Adapts an <see cref="IAgentTool"/> to an <see cref="AIFunction"/> the model can call.
/// </summary>
public static class AIFunctionAdapter
{
    public static AITool ToAIFunction(IAgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var function = new AgentToolFunction(tool);

        var policy = ToolPolicyReader.GetPolicy(tool);
        if (policy.RequiresApproval)
        {
            return new ApprovalRequiredAIFunction(function);
        }

        return function;
    }

    private sealed class AgentToolFunction(IAgentTool tool) : AIFunction
    {
        private readonly JsonElement schema = BuildSchema(tool.Parameters);

        public override string Name => tool.Name;

        public override string Description => tool.Description;

        public override JsonElement JsonSchema => schema;

        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(arguments);

            var input = ResolveInput(arguments);
            return await tool.ExecuteAsync(input, cancellationToken);
        }

        private string ResolveInput(AIFunctionArguments arguments)
        {
            var parameters = tool.Parameters;

            if (parameters.Count > 1)
            {
                return JsonSerializer.Serialize(arguments.ToDictionary(kv => kv.Key, kv => kv.Value));
            }

            if (parameters.Count == 1 && arguments.TryGetValue(parameters[0].Name, out var byName))
            {
                return CoerceValue(byName) ?? string.Empty;
            }

            return arguments.Count == 1 ? CoerceValue(arguments.Values.First()) ?? string.Empty : string.Empty;
        }
    }

    private static JsonElement BuildSchema(IReadOnlyList<AgentToolParameter> parameters)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var parameter in parameters)
        {
            var property = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["type"] = JsonTypeName(parameter.Type),
                ["description"] = parameter.Description,
            };

            if (parameter.EnumValues is { Count: > 0 })
            {
                property["enum"] = parameter.EnumValues;
            }

            properties[parameter.Name] = property;

            if (parameter.Required)
            {
                required.Add(parameter.Name);
            }
        }

        var schema = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string JsonTypeName(AgentToolParameterType type) => type switch
    {
        AgentToolParameterType.Integer => "integer",
        AgentToolParameterType.Number => "number",
        AgentToolParameterType.Boolean => "boolean",
        _ => "string",
    };

    private static string? CoerceValue(object? value) => value switch
    {
        null => null,
        string s => s,
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => je.GetRawText(),
        },
        _ => value.ToString(),
    };
}
