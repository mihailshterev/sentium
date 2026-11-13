namespace Sentium.Praxis.Features.Agents.Native;

/// <summary>
/// The squad synthesizer: a control agent that combines the specialists' step results into the single final answer.
/// </summary>
public sealed class SummaryAgent : IAgent
{
    /// <remarks>
    /// Must equal the key this agent is registered under (<see cref="AgentRole.Summarizer"/>).
    /// </remarks>
    public string Name => AgentRole.Summarizer;

    public string Description => "Combines step results into one final answer.";

    public IReadOnlyList<string>? Tools => [];

    public string Instructions =>
        """
        You combine the results of a team of specialists into one final answer to the user's request.
        Keep every correct detail, remove repetition, resolve contradictions in favour of verified results, and do not
        add new claims. Use clear markdown structure where it helps. Output only the final answer.
        """;
}
