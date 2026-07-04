namespace Sentium.AgentRuntime.Core.Tools;

/// <summary>
/// Defines the contract for an executable capability that an agent can invoke.
/// Tools allow agents to interact with external systems, perform computations,
/// or retrieve real-time data.
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// This is the identifier the LLM uses to select and call the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a detailed description of what the tool does and when it should be used.
    /// High-quality descriptions are critical for the LLM to understand the tool's utility.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the arguments this tool accepts. These are turned into the JSON schema advertised to the
    /// model, so it knows exactly what to send. Tools that take no arguments leave this empty (the
    /// default). The parameter names must match what <see cref="ExecuteAsync"/> expects to parse out of
    /// its input: for a single parameter the raw value is passed through; for multiple parameters the
    /// arguments are passed as a JSON object whose property names are these parameter names.
    /// </summary>
    IReadOnlyList<AgentToolParameter> Parameters => [];

    /// <summary>
    /// Executes the tool's logic asynchronously.
    /// </summary>
    /// <param name="input">The raw input string provided by the agent (usually JSON-formatted arguments).</param>
    /// <param name="ct">A cancellation token to monitor for request cancellation.</param>
    /// <returns>A string representation of the tool's output, to be fed back into the agent's context.</returns>
    Task<string> ExecuteAsync(string input, CancellationToken ct);
}
