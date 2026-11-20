using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// Applies an <see cref="UnattendedRunPolicy"/> to an agent's tools. A pre-approved gated tool is re-exposed without its
/// approval marker, so it runs without pausing but still passes the Sentinel middleware. Any other gated tool is
/// replaced by a stub with the same name and schema that answers with a denial, so the agent can report what it could
/// not do and the run never waits for a human.
/// </summary>
/// <remarks>
/// Function invocation detects a gated tool through <c>GetService&lt;ApprovalRequiredAIFunction&gt;()</c>, which a
/// <see cref="DelegatingAIFunction"/> forwards to its inner function. The wrappers here derive from
/// <see cref="AIFunction"/> directly so the marker stays hidden.
/// </remarks>
internal static class UnattendedToolGate
{
    private static readonly IReadOnlyDictionary<string, object?> NoProperties = new Dictionary<string, object?>();

    public static AITool Apply(AITool tool, UnattendedRunPolicy policy)
    {
        if (tool.GetService<ApprovalRequiredAIFunction>() is not { } gated)
        {
            return tool;
        }

        return policy.IsPreApproved(gated.Name) ? new PreApprovedFunction(gated) : new DeniedFunction(gated);
    }

    public static string DenialFor(string toolName) =>
        $"[Not permitted] '{toolName}' requires approval, and this unattended scheduled run has no pre-approval for it. " +
        "Do not retry it; continue without it and say in your answer what you could not do.";

    private abstract class ForwardingFunction(AIFunction source) : AIFunction
    {
        protected AIFunction Source => source;

        public override string Name => source.Name;

        public override string Description => source.Description;

        public override JsonElement JsonSchema => source.JsonSchema;

        public override JsonElement? ReturnJsonSchema => source.ReturnJsonSchema;

        public override JsonSerializerOptions JsonSerializerOptions => source.JsonSerializerOptions;

        public override MethodInfo? UnderlyingMethod => source.UnderlyingMethod;
    }

    /// <summary>
    /// Invoking the approval wrapper directly runs the tool: the approval step lives in function invocation, not in the
    /// function.
    /// </summary>
    private sealed class PreApprovedFunction(AIFunction gated) : ForwardingFunction(gated)
    {
        public override IReadOnlyDictionary<string, object?> AdditionalProperties => Source.AdditionalProperties;

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
            Source.InvokeAsync(arguments, cancellationToken);
    }

    /// <summary>
    /// Carries no Sentinel classification, so the PDP is not asked about a call that never runs.
    /// </summary>
    private sealed class DeniedFunction(AIFunction gated) : ForwardingFunction(gated)
    {
        public override IReadOnlyDictionary<string, object?> AdditionalProperties => NoProperties;

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) =>
            ValueTask.FromResult<object?>(DenialFor(Name));
    }
}
