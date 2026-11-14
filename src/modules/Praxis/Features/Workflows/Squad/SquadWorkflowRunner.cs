using Microsoft.Agents.AI.Workflows;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.Orchestration;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Runs a squad as an Agent Framework graph workflow designed for small local models:
/// <code>
/// [Planner] ─▶ Step ─▶ Check ─┬─ retry (fixes as feedback) ─▶ Step
///                             ├─ next step ────────────────▶ Step
///                             └─ done ──▶ Synthesis ─▶ output
/// </code>
/// Control decisions (plan, review, assessment) use JSON-schema constrained decoding; every hand-off is one compact,
/// typed message instead of a shared chat transcript; each step is reviewed on its own (a narrow question small models
/// answer reliably) and retried at most once.
/// </summary>
internal sealed class SquadWorkflowRunner(IAgentFactory factory)
{
    /// <summary>
    /// First attempt plus one revision.
    /// </summary>
    public const int MaxAttemptsPerStep = 2;

    /// <param name="trigger">The run trigger (stream id, acting user).</param>
    /// <param name="request">The user's request.</param>
    /// <param name="roster">Agents available to the run.</param>
    /// <param name="fixedPlan">A predefined plan (custom workflows), or <see langword="null"/> to let the planner decide.</param>
    /// <param name="timeline">The run's timeline; the caller reads its entries into the run history.</param>
    public async Task<SquadResult> RunAsync(
        WorkflowTrigger trigger,
        string request,
        IReadOnlyList<SquadMember> roster,
        SquadPlan? fixedPlan,
        WorkflowTimeline timeline,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(roster);

        var run = new SquadRunContext(factory, timeline, trigger.UserId, roster);

        var step = new StepExecutor(run);
        var check = new StepCheckExecutor(run, MaxAttemptsPerStep);
        var synthesis = new SynthesisExecutor(run);

        if (fixedPlan is null)
        {
            var planner = new PlannerExecutor(run);
            var workflow = WireLoop(new WorkflowBuilder(planner), step, check, synthesis)
                .AddEdge<object?>(planner, step, m => m is StepRequest)
                .AddEdge<object?>(planner, synthesis, m => m is SquadOutcome)
                .Build();

            return await ExecuteAsync(workflow, request, ct);
        }

        var state = new SquadState(request, fixedPlan, []);
        if (fixedPlan.Steps.Count == 0)
        {
            var empty = WireLoop(new WorkflowBuilder(synthesis), step, check, synthesis).Build();
            return await ExecuteAsync(empty, new SquadOutcome(state), ct);
        }

        var fixedWorkflow = WireLoop(new WorkflowBuilder(step), step, check, synthesis).Build();
        return await ExecuteAsync(fixedWorkflow, new StepRequest(state, 0, 1, [], null), ct);
    }

    private static WorkflowBuilder WireLoop(WorkflowBuilder builder, StepExecutor step, StepCheckExecutor check, SynthesisExecutor synthesis)
    {
        return builder
            .AddEdge(step, check)
            .AddEdge<object?>(check, step, m => m is StepRequest)
            .AddEdge<object?>(check, synthesis, m => m is SquadOutcome)
            .WithOutputFrom(synthesis);
    }

    private static async Task<SquadResult> ExecuteAsync<TInput>(Workflow workflow, TInput input, CancellationToken ct)
        where TInput : notnull
    {
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, input, cancellationToken: ct);

        SquadResult? result = null;
        await foreach (var evt in run.WatchStreamAsync(ct))
        {
            switch (evt)
            {
                case WorkflowOutputEvent output when output.Is<SquadResult>(out var squadResult):
                    result = squadResult;
                    break;
                case ExecutorFailedEvent failed:
                    throw Rethrow(failed.Data as Exception, $"Squad executor '{failed.ExecutorId}' failed.", ct);
                case WorkflowErrorEvent error:
                    throw Rethrow(error.Exception, "The squad workflow failed.", ct);
            }
        }

        ct.ThrowIfCancellationRequested();
        return result ?? throw new InvalidOperationException("The squad workflow finished without producing a result.");
    }

    /// <summary>
    /// Keeps cancellation a cancellation (the run processor distinguishes user stops and timeouts from failures).
    /// </summary>
    private static Exception Rethrow(Exception? inner, string message, CancellationToken ct)
    {
        if (inner is OperationCanceledException canceled)
        {
            return canceled;
        }

        ct.ThrowIfCancellationRequested();
        return new InvalidOperationException(message, inner);
    }
}
