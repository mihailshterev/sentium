using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Tools;

/// <summary>
/// Binds an <see cref="IAgentTool"/>'s typed method to an <see cref="AIFunction"/> via <see cref="AIFunctionFactory"/>,
/// stamping the tool's Sentinel classification onto <see cref="AITool.AdditionalProperties"/> and wrapping it in
/// <see cref="ApprovalRequiredAIFunction"/> when its policy requires human approval.
/// </summary>
public static class AgentToolFunction
{
    private const string ResourceKey = "sentium.tool.resource";
    private const string ActionKey = "sentium.tool.action";

    /// <summary>
    /// Argument marshalling tuned for small local models, which often quote numbers or vary enum casing:
    /// numbers are accepted from strings and enums are read by (case-insensitive) name.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static AIFunction Create(IAgentTool tool, Delegate method, string name, string description)
    {
        ArgumentNullException.ThrowIfNull(tool);

        var policy = ToolPolicyReader.GetPolicy(tool);

        var function = AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
        {
            Name = name,
            Description = description,
            SerializerOptions = SerializerOptions,
            AdditionalProperties = new Dictionary<string, object?>
            {
                [ResourceKey] = policy.Resource.ToString(),
                [ActionKey] = policy.Action.ToString(),
            },
        });

        return policy.RequiresApproval ? new ApprovalRequiredAIFunction(function) : function;
    }

    /// <summary>
    /// Name of the agent whose run is invoking the tool, from the framework's ambient <see cref="AIAgent.CurrentRunContext"/>.
    /// </summary>
    public static string CurrentAgentName => AIAgent.CurrentRunContext?.Agent.Name is { Length: > 0 } name ? name : "Agent";

    /// <summary>
    /// Reads the Sentinel classification of a Sentium tool. Returns <see langword="false"/> for functions that are not
    /// Sentium tools (e.g. the framework's skill functions).
    /// </summary>
    public static bool TryGetClassification(AIFunction function, [NotNullWhen(true)] out string? resource, [NotNullWhen(true)] out string? action)
    {
        ArgumentNullException.ThrowIfNull(function);

        resource = function.AdditionalProperties.TryGetValue(ResourceKey, out var r) ? r as string : null;
        action = function.AdditionalProperties.TryGetValue(ActionKey, out var a) ? a as string : null;

        return resource is not null && action is not null;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        if (!options.Converters.Any(c => c is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }

        options.MakeReadOnly();
        return options;
    }
}
