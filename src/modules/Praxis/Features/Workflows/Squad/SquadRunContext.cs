using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Orchestration;

namespace Sentium.Praxis.Features.Workflows.Squad;

/// <summary>
/// Per-run services shared by the squad executors: agent construction, live streaming to the run's UI timeline, and
/// structured (constrained-decoding) calls. Holds no workflow state - that travels in the graph's messages.
/// </summary>
internal sealed class SquadRunContext(
    IAgentFactory factory,
    IAgentStreamPublisher stream,
    StreamLogAccumulator log,
    string streamId,
    Guid? userId,
    IReadOnlyList<SquadMember> roster)
{
    public const string SystemAuthor = "System";

    private static readonly JsonSerializerOptions StructuredJson = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, AIAgent> specialists = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<SquadMember> Roster => roster;

    public SquadMember? FindMember(string name) =>
        roster.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Status(string author, string text)
    {
        stream.Emit(streamId, author, text, AgentUpdateTypes.Status);
        log.Add(author, text, AgentUpdateTypes.Status);
    }

    /// <summary>
    /// A specialist equipped for a team step: its role plus the step rules, its curated tools minus approval-gated ones
    /// (no human can approve inside a workflow). Built once per run and reused across retries.
    /// </summary>
    public async ValueTask<AIAgent> GetSpecialistAsync(SquadMember member, CancellationToken ct)
    {
        if (!specialists.TryGetValue(member.Name, out var agent))
        {
            agent = await factory.CreateAsync(
                member.Name,
                overrideInstructions: $"{member.Instructions.Trim()}\n\n{SquadPrompts.StepRules}",
                overrideModel: member.Model,
                actingUserId: userId,
                allowApprovalTools: false,
                ct: ct);

            specialists[member.Name] = agent;
        }

        return agent;
    }

    public Task<AIAgent> CreateControlAgentAsync(string agentName, string? instructions, CancellationToken ct) =>
        factory.CreateControlAgentAsync(agentName, instructions, actingUserId: userId, ct: ct);

    /// <summary>
    /// Runs <paramref name="agent"/> on a single input message in a fresh session, streaming reasoning, tool calls, and
    /// text to the timeline under <paramref name="author"/>. Returns the full text.
    /// </summary>
    public async Task<string> StreamAsync(AIAgent agent, string author, string input, CancellationToken ct)
    {
        var session = await agent.CreateSessionAsync(ct);
        var run = new AgentRunStream(new RelayUpdateSink(stream, log, streamId, author));

        await run.RunAsync(agent.RunStreamingAsync(input, session, cancellationToken: ct), ct);

        return run.Text;
    }

    /// <summary>
    /// Runs a control agent with a JSON-schema response format - llama-server compiles the schema to a grammar that constrains decoding - and
    /// deserializes the answer. Returns <see langword="default"/> when the model's answer cannot be read.
    /// </summary>
    public async Task<T?> RunStructuredAsync<T>(AIAgent agent, string input, CancellationToken ct, ChatResponseFormat? format = null)
        where T : class
    {
        var response = await agent.RunAsync(
            input,
            options: new AgentRunOptions { ResponseFormat = format ?? ChatResponseFormat.ForJsonSchema<T>(StructuredJson) },
            cancellationToken: ct);

        try
        {
            return JsonSerializer.Deserialize<T>(StripCodeFence(response.Text), StructuredJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        var closing = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd > 0 && closing > firstLineEnd ? trimmed[(firstLineEnd + 1)..closing].Trim() : trimmed;
    }
}
