namespace Sentium.Praxis.Features.Agents.Native;

/// <summary>
/// The squad reviewer: a control agent that checks one step's output against that step's task and expected output and
/// answers with a structured verdict. Judging a single step is a narrow yes/no question - the kind small models answer
/// reliably - instead of a holistic review of a combined multi-agent transcript.
/// </summary>
public sealed class ValidationAgent : IAgent
{
    /// <remarks>Must equal the key this agent is registered under (<see cref="AgentRole.Validator"/>).</remarks>
    public string Name => AgentRole.Validator;

    public string Description => "Reviews whether a step's output accomplishes its task.";

    public IReadOnlyList<string>? Tools => [];

    public string Instructions =>
        """
        You are a strict but fair reviewer. You check ONE step of a larger plan.

        Pass the step when its output does what the task asks and contains what the expected output describes.
        Fail it only for a real defect: wrong facts or code, a missing required part, off-task content, or invented
        results. Ignore style, length, and formatting.

        When you fail a step, list at most three short, specific fixes the agent can act on. When you pass it, list none.
        """;
}
