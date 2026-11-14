using Microsoft.Agents.AI.Workflows;
using Sentium.Praxis.Features.Orchestration;

namespace Sentium.Praxis.Features.Workflows.Squad;

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

        run.Status(WorkflowTimeline.SystemAuthor, message.Attempt == 1
            ? $"{position} - {step.Agent}: {step.Task}"
            : $"{position} - {step.Agent}: revising (attempt {message.Attempt})");

        var member = run.FindMember(step.Agent)
            ?? throw new InvalidOperationException($"Planned agent '{step.Agent}' is not in the run's roster.");

        var agent = await run.GetSpecialistAsync(member, cancellationToken);
        var output = await run.StreamAsync(agent, member.Name, SquadPrompts.StepInput(message), cancellationToken);

        await context.SendMessageAsync(new StepOutput(message, output), cancellationToken);
    }
}
