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
    /// Executes the tool's logic asynchronously.
    /// </summary>
    /// <param name="input">The raw input string provided by the agent (usually JSON-formatted arguments).</param>
    /// <param name="ct">A cancellation token to monitor for request cancellation.</param>
    /// <returns>A string representation of the tool's output, to be fed back into the agent's context.</returns>
    Task<string> ExecuteAsync(string input, CancellationToken ct);
}
