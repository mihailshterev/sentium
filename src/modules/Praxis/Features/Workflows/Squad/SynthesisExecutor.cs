using Microsoft.Agents.AI.Workflows;
using Sentium.Praxis.Features.Agents;

namespace Sentium.Praxis.Features.Workflows.Squad;

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
