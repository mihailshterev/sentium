using Microsoft.Agents.AI.Workflows;
using Sentium.Praxis.Features.Agents;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Plans the run: the Orchestrator answers with a JSON plan constrained to the roster.
/// Sends the first <see cref="StepRequest"/>, or a <see cref="SquadOutcome"/> when no work is needed.
/// </summary>
internal sealed class PlannerExecutor(SquadRunContext run) : Executor<string>("planner")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        base.ConfigureProtocol(protocolBuilder).SendsMessage<StepRequest>().SendsMessage<SquadOutcome>();

    public override async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        run.Status(AgentRole.Orchestrator, "Planning which specialists handle this request");

        var planner = await run.CreateControlAgentAsync(AgentRole.Orchestrator, instructions: null, cancellationToken);
        var plan = await run.RunStructuredAsync<SquadPlan>(
            planner,
            SquadPrompts.PlannerInput(message, run.Roster),
            cancellationToken,
            SquadPrompts.PlanFormat(run.Roster));

        var steps = Sanitize(plan);
        if (plan is null)
        {
            steps = Fallback(message);
            run.Status(SquadRunContext.SystemAuthor, "The plan could not be read - continuing with a single general step.");
        }

        var state = new SquadState(message, new SquadPlan(steps), []);

        if (steps.Count == 0)
        {
            run.Status(AgentRole.Orchestrator, "No specialist work is needed for this request.");
            await context.SendMessageAsync(new SquadOutcome(state), cancellationToken);
            return;
        }

        run.Status(AgentRole.Orchestrator, $"Plan: {SquadPrompts.DescribePlan(state.Plan)}");
        await context.SendMessageAsync(new StepRequest(state, 0, 1, [], null), cancellationToken);
    }

    /// <summary>
    /// Keeps only well-formed steps for roster agents (canonical names), capped at <see cref="SquadPrompts.MaxPlanSteps"/>.
    /// Constrained decoding already enforces this; the check guards models or servers that ignore the schema.
    /// </summary>
    private List<SquadStep> Sanitize(SquadPlan? plan) =>
    [
        .. (plan?.Steps ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Task))
            .Select(s => run.FindMember(s.Agent) is { } member ? s with { Agent = member.Name } : null)
            .OfType<SquadStep>()
            .Take(SquadPrompts.MaxPlanSteps)
    ];

    private List<SquadStep> Fallback(string request)
    {
        var member = run.FindMember(AgentRole.GeneralAssistant) ?? run.Roster.FirstOrDefault();
        return member is null ? [] : [new SquadStep(member.Name, request, "A complete, correct answer to the request.")];
    }
}

/// <summary>
/// Runs one step: the step's specialist receives a single compact message (request, task, expected output, earlier
/// results, reviewer feedback) in a fresh session - no shared transcript, so no persona bleed between agents.
/// </summary>
internal sealed class StepExecutor(SquadRunContext run) : Executor<StepRequest>("step")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        base.ConfigureProtocol(protocolBuilder).SendsMessage<StepOutput>();

    public override async ValueTask HandleAsync(StepRequest message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var step = message.Step;
        var position = $"Step {message.StepIndex + 1}/{message.State.Plan.Steps.Count}";

        run.Status(SquadRunContext.SystemAuthor, message.Attempt == 1
            ? $"{position} - {step.Agent}: {step.Task}"
            : $"{position} - {step.Agent}: revising (attempt {message.Attempt})");

        var member = run.FindMember(step.Agent)
            ?? throw new InvalidOperationException($"Planned agent '{step.Agent}' is not in the run's roster.");

        var agent = await run.GetSpecialistAsync(member, cancellationToken);
        var output = await run.StreamAsync(agent, member.Name, SquadPrompts.StepInput(message), cancellationToken);

        await context.SendMessageAsync(new StepOutput(message, output), cancellationToken);
    }
}

/// <summary>
/// Reviews one step and routes the run: retry the step with the reviewer's fixes, advance to the next step, or finish.
/// Cheap deterministic checks run before the model; a step is retried at most <paramref name="maxAttempts"/> - 1 times
/// and accepted as unverified when the budget is spent or the model repeats itself.
/// </summary>
internal sealed class StepCheckExecutor(SquadRunContext run, int maxAttempts) : Executor<StepOutput>("check")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        base.ConfigureProtocol(protocolBuilder).SendsMessage<StepRequest>().SendsMessage<SquadOutcome>();

    public override async ValueTask HandleAsync(StepOutput message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var request = message.Request;
        var label = $"Step {request.StepIndex + 1}";

        var stuck = request.PreviousOutput is not null && OutputSimilarity.IsStuck(request.PreviousOutput, message.Output);
        var verdict = await ReviewAsync(request.Step, message.Output, cancellationToken);
        var passed = verdict?.Passed == true;

        if (verdict is { Passed: false } && !stuck && request.Attempt < maxAttempts)
        {
            run.Status(AgentRole.Validator, $"{label} needs revision: {string.Join("; ", verdict.Issues)}");
            await context.SendMessageAsync(
                request with { Attempt = request.Attempt + 1, Feedback = verdict.Issues, PreviousOutput = message.Output },
                cancellationToken);
            return;
        }

        if (passed)
        {
            run.Status(AgentRole.Validator, $"{label} passed review");
        }
        else if (verdict is null)
        {
            // An unreadable review must not block the run; accept the step as unverified.
            run.Status(SquadRunContext.SystemAuthor, $"{label}: the review could not be read - accepting the step as is.");
        }
        else if (stuck)
        {
            run.Status(SquadRunContext.SystemAuthor, $"{label}: the revision repeated the previous attempt - keeping it and moving on.");
        }
        else
        {
            run.Status(AgentRole.Validator, $"{label} still has issues after {maxAttempts} attempts - keeping the best effort.");
        }

        var state = request.State with
        {
            Completed = [.. request.State.Completed, new StepResult(request.Step, message.Output, passed)],
        };

        var next = request.StepIndex + 1;
        if (next < state.Plan.Steps.Count)
        {
            await context.SendMessageAsync(new StepRequest(state, next, 1, [], null), cancellationToken);
        }
        else
        {
            await context.SendMessageAsync(new SquadOutcome(state), cancellationToken);
        }
    }

    /// <summary>
    /// The step's verdict, or <see langword="null"/> when the reviewer's answer could not be read.
    /// </summary>
    private async Task<StepVerdict?> ReviewAsync(SquadStep step, string output, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new StepVerdict(false, ["The step produced no output - produce the result the task asks for."]);
        }

        var reviewer = await run.CreateControlAgentAsync(AgentRole.Validator, instructions: null, ct);
        var verdict = await run.RunStructuredAsync<StepVerdict>(reviewer, SquadPrompts.CheckInput(step, output), ct);

        return verdict is null
            ? null
            : verdict with { Issues = [.. (verdict.Issues ?? []).Where(i => !string.IsNullOrWhiteSpace(i)).Take(3)] };
    }
}

/// <summary>
/// Produces the final answer (the single step's output, or a synthesis of several) plus a structured risk assessment,
/// and yields the <see cref="SquadResult"/> as the workflow output.
/// </summary>
internal sealed class SynthesisExecutor(SquadRunContext run) : Executor<SquadOutcome>("synthesis")
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        base.ConfigureProtocol(protocolBuilder).YieldsOutput<SquadResult>();

    public override async ValueTask HandleAsync(SquadOutcome message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var state = message.State;
        var results = state.Completed;

        string answer;
        if (results.Count == 0)
        {
            answer = string.Empty;
        }
        else if (results.Count == 1)
        {
            answer = results[0].Output;
        }
        else
        {
            run.Status(AgentRole.Summarizer, "Combining the team's results into the final answer");
            var synthesizer = await run.CreateControlAgentAsync(AgentRole.Summarizer, instructions: null, cancellationToken);
            answer = await run.StreamAsync(synthesizer, AgentRole.Summarizer, SquadPrompts.SynthesisInput(state.Request, results), cancellationToken);
        }

        var assessment = answer.Length == 0
            ? new FinalAssessment(RiskLevel.Low, "No specialist work was required for this request.")
            : await AssessAsync(state.Request, answer, cancellationToken);

        await context.YieldOutputAsync(new SquadResult(answer, assessment.Risk, assessment.Recommendation, results), cancellationToken);
    }

    private async Task<FinalAssessment> AssessAsync(string request, string answer, CancellationToken ct)
    {
        var assessor = await run.CreateControlAgentAsync(AgentRole.Validator, SquadPrompts.AssessmentInstructions, ct);
        var assessment = await run.RunStructuredAsync<FinalAssessment>(assessor, SquadPrompts.AssessmentInput(request, answer), ct);

        return assessment ?? new FinalAssessment(RiskLevel.Medium, "Review the answer manually - the automatic assessment could not be read.");
    }
}
