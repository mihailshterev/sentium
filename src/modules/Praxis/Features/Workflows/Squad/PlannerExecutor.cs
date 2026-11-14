using Microsoft.Agents.AI.Workflows;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;

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
            run.Status(WorkflowTimeline.SystemAuthor, "The plan could not be read - continuing with a single general step.");
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
    private List<SquadStep> Sanitize(SquadPlan? plan)
    {
        return [
            .. (plan?.Steps ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s.Task))
                .Select(s => run.FindMember(s.Agent) is { } member ? s with { Agent = member.Name } : null)
                .OfType<SquadStep>()
                .Take(SquadPrompts.MaxPlanSteps)
        ];
    }

    private List<SquadStep> Fallback(string request)
    {
        var member = run.FindMember(AgentRole.GeneralAssistant) ?? run.Roster.FirstOrDefault();
        return member is null ? [] : [new SquadStep(member.Name, request, "A complete, correct answer to the request.")];
    }
}
