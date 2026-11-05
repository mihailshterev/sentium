using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// An agent available to a squad run.
/// </summary>
/// <param name="Name">Agent name (native role or the user's database agent).</param>
/// <param name="Description">One-line summary shown to the planner.</param>
/// <param name="Instructions">The agent's role instructions.</param>
/// <param name="Model">Model override, or <see langword="null"/> for the configured default.</param>
public sealed record SquadMember(string Name, string Description, string Instructions, string? Model = null);

/// <summary>
/// One planned step: which agent does what, and what a correct result contains.
/// </summary>
public sealed record SquadStep(
    [property: Description("The agent that performs this step, exactly as named in the roster.")] string Agent,
    [property: Description("One concrete instruction telling the agent what to produce.")] string Task,
    [property: Description("What a correct result for this step contains.")] string ExpectedOutput);

/// <summary>
/// The ordered plan a squad executes.
/// </summary>
public sealed record SquadPlan([property: Description("The steps, in execution order.")] IReadOnlyList<SquadStep> Steps);

/// <summary>
/// A finished step. <see cref="Verified"/> is <see langword="false"/> when the output was accepted without passing review
/// (retry budget exhausted or the model stopped changing its answer).
/// </summary>
public sealed record StepResult(SquadStep Step, string Output, bool Verified);

/// <summary>
/// Risk of acting on a squad's answer.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<RiskLevel>))]
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// The outcome of a squad run.
/// </summary>
public sealed record SquadResult(string Answer, RiskLevel Risk, string Recommendation, IReadOnlyList<StepResult> Steps);

/// <summary>
/// Structured verdict for a single step (constrained decoding).
/// </summary>
public sealed record StepVerdict(
    [property: Description("True when the output accomplishes the task and contains what the expected output describes.")] bool Passed,
    [property: Description("When not passed: at most three short, specific fixes. Empty when passed.")] IReadOnlyList<string> Issues);

/// <summary>
/// Structured assessment of the final answer (constrained decoding).
/// </summary>
public sealed record FinalAssessment(
    [property: Description("Risk of acting on the answer, considering accuracy and completeness.")] RiskLevel Risk,
    [property: Description("One-sentence recommendation for the user.")] string Recommendation);

// ---- Workflow messages: immutable, so the graph carries all run state and executors stay stateless. ----

/// <summary>Run state threaded through the graph.</summary>
internal sealed record SquadState(string Request, SquadPlan Plan, IReadOnlyList<StepResult> Completed);

/// <summary>Instructs the step executor to run (or re-run) one step.</summary>
internal sealed record StepRequest(SquadState State, int StepIndex, int Attempt, IReadOnlyList<string> Feedback, string? PreviousOutput)
{
    public SquadStep Step => State.Plan.Steps[StepIndex];
}

/// <summary>A step's raw output, on its way to review.</summary>
internal sealed record StepOutput(StepRequest Request, string Output);

/// <summary>All steps finished; ready for synthesis.</summary>
internal sealed record SquadOutcome(SquadState State);
