using System.Text.Json;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.WorkflowManagement;
using Sentium.Praxis.Features.Workflows.Squad;
using AgentResponse = Sentium.Praxis.Features.Agents.AgentResponse;

namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// User-defined squad: a fixed plan - the workflow's agents, in their configured order - each contributing its role.
/// </summary>
internal sealed class DynamicCustomWorkflow(
    SquadWorkflowRunner squad,
    IAgentRepository agentRepository,
    IWorkflowService workflowService,
    IAgentStreamPublisher stream) : IAgentWorkflow
{
    public WorkflowType Type => WorkflowType.Custom;

    public async Task<WorkflowResult> ExecuteAsync(WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (!TryReadWorkflowId(trigger.Payload, out var workflowId))
        {
            return new WorkflowResult { Explanation = "Custom workflow trigger is missing a valid workflowId." };
        }

        var workflowDef = await workflowService.GetWorkflowAsync(workflowId, ct);
        if (workflowDef is null)
        {
            return new WorkflowResult { Explanation = $"Workflow '{workflowId}' was not found." };
        }

        var agents = new List<AgentResponse>();
        foreach (var agentRef in workflowDef.Agents.OrderBy(a => a.Order))
        {
            if (await agentRepository.GetAgentByIdAsync(agentRef.AgentId, ct) is { } agent)
            {
                agents.Add(agent);
            }
        }

        if (agents.Count == 0)
        {
            return new WorkflowResult { Explanation = $"Workflow '{workflowDef.Name}' has no agents configured." };
        }

        var timeline = new WorkflowTimeline(stream, trigger.StreamId);
        WarnOnDuplicateNames(timeline, workflowDef.Name, agents);

        var roster = agents
            .DistinctBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a => new SquadMember(a.Name, a.Description, a.Description, string.IsNullOrWhiteSpace(a.Model) ? null : a.Model))
            .ToList();

        var plan = new SquadPlan([.. agents.Select(a => new SquadStep(
            a.Name,
            "Contribute the part of the request that your role covers.",
            $"A complete, correct contribution for this role: {Summarize(a.Description)}"))]);

        var request = WorkspaceContextPrompt.Augment(trigger.ResolveActivity(), WorkspaceContextPrompt.TryExtract(trigger.Payload));
        var result = await squad.RunAsync(trigger, request, roster, plan, timeline, ct);

        return SquadResultMapper.ToWorkflowResult(result, timeline, $"Custom workflow '{workflowDef.Name}' completed with no output.", workflowId, workflowDef.UserId);
    }

    private static void WarnOnDuplicateNames(WorkflowTimeline timeline, string workflowName, IReadOnlyList<AgentResponse> agents)
    {
        var duplicates = agents.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count == 0)
        {
            return;
        }

        var warning = $"Warning: workflow '{workflowName}' has duplicate agent names ({string.Join(", ", duplicates)}). Per-agent attribution may be unreliable - give each agent a unique name.";
        timeline.Post(WorkflowTimeline.SystemAuthor, warning, AgentUpdateTypes.Status);
    }

    private static bool TryReadWorkflowId(string payload, out Guid workflowId)
    {
        workflowId = Guid.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("workflowId", out var id) && id.TryGetGuid(out workflowId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Summarize(string? text)
    {
        var oneLine = (text ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return oneLine.Length > 200 ? string.Concat(oneLine.AsSpan(0, 200), "…") : oneLine;
    }
}
