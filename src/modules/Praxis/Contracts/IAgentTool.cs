using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Contracts;

/// <summary>
/// An executable capability an agent can invoke. Implementations expose one typed, <c>[Description]</c>-annotated method
/// and bind it with <see cref="AgentToolFunction.Create"/>, which lets <see cref="AIFunctionFactory"/> derive the JSON
/// schema and marshal the model's arguments - no hand-written schemas or argument parsing.
/// </summary>
/// <remarks>
/// Usage policy (allowed agents, approval, Sentinel classification) is declared with
/// <see cref="AgentToolPolicyAttribute"/> on the implementing class.
/// </remarks>
public interface IAgentTool
{
    /// <summary>
    /// Creates the function advertised to the model.
    /// </summary>
    AIFunction CreateFunction();
}
