using Microsoft.Agents.AI.Workflows;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;

namespace Sentium.Praxis.Features.Workflows.Squad;

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
            run.Status(WorkflowTimeline.SystemAuthor, $"{label}: the review could not be read - accepting the step as is.");
        }
        else if (stuck)
        {
            run.Status(WorkflowTimeline.SystemAuthor, $"{label}: the revision repeated the previous attempt - keeping it and moving on.");
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
