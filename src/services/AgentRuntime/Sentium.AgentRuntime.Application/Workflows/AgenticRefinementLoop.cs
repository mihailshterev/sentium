using System.Text;
using Sentium.AgentRuntime.Application.Common.Helpers;
using Sentium.AgentRuntime.Application.Extensions;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Workflows;
using Sentium.Infrastructure.Messaging;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.AgentRuntime.Application.Workflows;

internal static class RefinementStopReason
{
    public const string Passed = "passed";
    public const string Stuck = "stuck";
    public const string MaxIterations = "max_iterations";
}

internal sealed record RefinementOutcome(
    List<ChatMessage> FinalHistory,
    string SquadText,
    string ValidatorOutput,
    int Iterations,
    string StopReason);

internal sealed record SquadMember(string Name, string Role);

/// <summary>
/// A self-correcting agentic loop. It runs the sequential squad,
/// has a Validator agent judge the output, and - if rejected - feeds the critique back and re-runs the squad,
/// up to a small iteration budget. It aborts early on a stuck state (near-identical consecutive output), prunes
/// context aggressively to protect small context windows, and streams every phase shift to NATS for the UI timeline.
/// </summary>
/// <remarks>Shared by <see cref="DynamicDiscoveryWorkflow"/> and <see cref="DynamicCustomWorkflow"/>.</remarks>
internal sealed class AgenticRefinementLoop(IAgentFactory factory, IEventBus nats)
{
    private const int DefaultMaxIterations = 3;
    private const int SquadTextBudget = 8000;
    private const int RoleSummaryBudget = 400;

    public async Task<RefinementOutcome> RunAsync(
        WorkflowTrigger trigger,
        string workflowName,
        IReadOnlyList<AIAgent> squad,
        string originalInput,
        StreamLogAccumulator streamLog,
        CancellationToken ct,
        IReadOnlyList<SquadMember>? roster = null,
        int maxIterations = DefaultMaxIterations)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(squad);
        ArgumentNullException.ThrowIfNull(streamLog);

        var messages = new List<ChatMessage> { new(ChatRole.User, originalInput) };

        AIAgent? validator = null;
        List<ChatMessage> finalHistory = [];
        var squadText = string.Empty;
        var validatorOutput = string.Empty;
        string? previousCumulative = null;
        var stopReason = RefinementStopReason.MaxIterations;

        var squadNames = ResolveSquadNames(squad, roster);
        var activeSquad = squad;

        var agentOutputCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int iteration;
        for (iteration = 1; iteration <= maxIterations; iteration++)
        {
            var isSliced = activeSquad.Count < squad.Count;
            var attemptStatus = isSliced
                ? $"Squad execution - attempt {iteration}/{maxIterations} (re-running from {squadNames[^activeSquad.Count]} onward)"
                : $"Squad execution - attempt {iteration}/{maxIterations}";

            await EmitStatusAsync(trigger, "System", attemptStatus, streamLog, ct);

            (finalHistory, squadText) = await RunSquadAsync(trigger, workflowName, activeSquad, messages, streamLog, ct);

            MergeSquadOutputs(agentOutputCache, squadText);

            var cumulativeText = ReconstructTranscript(squadNames, agentOutputCache);

            if (OutputSimilarity.IsStuck(previousCumulative, cumulativeText))
            {
                await EmitStatusAsync(trigger, "System", "Stuck-state detected - squad repeated its previous output. Aborting to conserve compute.", streamLog, ct);
                stopReason = RefinementStopReason.Stuck;
                break;
            }

            previousCumulative = cumulativeText;

            validator ??= await factory.CreateAsync(AgentRole.Validator, actingUserId: trigger.UserId, ct: ct);
            var validationInput = BuildValidationInput(originalInput, cumulativeText, roster);
            validatorOutput = await RunValidatorAsync(trigger, validator, validationInput, streamLog, ct);

            var verdict = LlmParser.ParseValidationVerdict(validatorOutput);
            if (verdict.Passed)
            {
                await EmitStatusAsync(trigger, AgentRole.Validator, "Validation passed", streamLog, ct);
                stopReason = RefinementStopReason.Passed;
                break;
            }

            await EmitStatusAsync(trigger, AgentRole.Validator, "Validation failed - requesting revision", streamLog, ct);

            if (iteration >= maxIterations)
            {
                stopReason = RefinementStopReason.MaxIterations;
                break;
            }

            var flagged = LlmParser.ParseResponsibleAgents(validatorOutput, squadNames);
            activeSquad = SliceSquad(squad, squadNames, flagged);

            if (activeSquad.Count < squad.Count)
            {
                await EmitStatusAsync(trigger, "System", $"Critique points to {string.Join(", ", flagged)} - re-running from {squadNames[^activeSquad.Count]} onward so downstream agents reprocess the correction.", streamLog, ct);
            }

            await EmitStatusAsync(trigger, "System", "Rewriting response based on reviewer feedback", streamLog, ct);
            messages = BuildCorrectiveContext(originalInput, cumulativeText, verdict.Critique, iteration + 1);
        }

        var finalTranscript = ReconstructTranscript(squadNames, agentOutputCache);
        return new RefinementOutcome(finalHistory, finalTranscript.Length > 0 ? finalTranscript : squadText, validatorOutput, iteration, stopReason);
    }

    /// <summary>
    /// Rebuilds and runs the sequential squad for one turn, streaming every reasoning/tool/text update to NATS and
    /// the log accumulator. The workflow is rebuilt each iteration because it is cheap and stateless, which avoids
    /// any cross-run state leaking between correction passes.
    /// </summary>
    /// <returns>
    /// The final chat history plus an <b>author-attributed</b> transcript (each agent's text under a
    /// <c>### {Agent}</c> header).
    /// </returns>
    private async Task<(List<ChatMessage> FinalHistory, string SquadText)> RunSquadAsync(
        WorkflowTrigger trigger,
        string workflowName,
        IReadOnlyList<AIAgent> squad,
        List<ChatMessage> messages,
        StreamLogAccumulator streamLog,
        CancellationToken ct)
    {
        var squadWorkflow = AgentWorkflowBuilder.BuildSequential(workflowName, [.. squad]);
        var transcript = new List<(string Author, StringBuilder Text)>();
        List<ChatMessage> finalHistory = [];

        await using var run = await InProcessExecution.RunStreamingAsync(squadWorkflow, messages, cancellationToken: ct);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var evt in run.WatchStreamAsync(ct).ConfigureAwait(false).WithCancellation(ct))
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                var author = e.Update.AuthorName ?? "Squad";

                if (e.Update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning && !string.IsNullOrEmpty(reasoning.Text))
                {
                    await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, reasoning.Text, AgentUpdateTypes.Thought, ct);
                    streamLog.Add(author, reasoning.Text, AgentUpdateTypes.Thought);
                }

                foreach (var call in e.Update.Contents.OfType<FunctionCallContent>())
                {
                    var toolLabel = $"Calling {call.Name}...";
                    await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, toolLabel, AgentUpdateTypes.Tool, ct);
                    streamLog.Add(author, toolLabel, AgentUpdateTypes.Tool);
                }

                if (!string.IsNullOrEmpty(e.Update.Text))
                {
                    if (transcript.Count == 0 || transcript[^1].Author != author)
                    {
                        transcript.Add((author, new StringBuilder()));
                    }

                    transcript[^1].Text.Append(e.Update.Text);
                    await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, e.Update.Text, ct);
                    streamLog.Add(author, e.Update.Text, AgentUpdateTypes.Message);
                }
            }
            else if (evt is WorkflowOutputEvent outputEvt)
            {
                finalHistory = outputEvt.As<List<ChatMessage>>() ?? [];
            }
        }

        var attributed = new StringBuilder();
        foreach (var (author, text) in transcript)
        {
            var clean = TranscriptSanitizer.StripHandoffLines(text.ToString());
            if (clean.Length == 0)
            {
                continue;
            }

            if (attributed.Length > 0)
            {
                attributed.Append("\n\n");
            }

            attributed.Append("### ").Append(author).Append('\n').Append(clean);
        }

        return (finalHistory, attributed.ToString());
    }

    private static List<string> ResolveSquadNames(IReadOnlyList<AIAgent> squad, IReadOnlyList<SquadMember>? roster)
    {
        var names = new List<string>(squad.Count);
        for (var i = 0; i < squad.Count; i++)
        {
            var name = squad[i].Name;
            if (string.IsNullOrWhiteSpace(name) && roster is not null && i < roster.Count)
            {
                name = roster[i].Name;
            }

            names.Add(string.IsNullOrWhiteSpace(name) ? $"Agent{i + 1}" : name);
        }

        return names;
    }

    private static IReadOnlyList<AIAgent> SliceSquad(IReadOnlyList<AIAgent> squad, IReadOnlyList<string> squadNames, IReadOnlyList<string> flaggedNames)
    {
        var start = SquadSlicing.ComputeReflowStartIndex(squadNames, flaggedNames);

        if (start <= 0)
        {
            return squad;
        }

        var sliced = new List<AIAgent>(squad.Count - start);
        for (var i = start; i < squad.Count; i++)
        {
            sliced.Add(squad[i]);
        }

        return sliced;
    }

    private static void MergeSquadOutputs(Dictionary<string, string> cache, string squadText)
    {
        if (string.IsNullOrWhiteSpace(squadText))
        {
            return;
        }

        const string firstPrefix = "### ";
        const string sectionSep = "\n\n### ";

        var normalized = squadText.StartsWith(firstPrefix, StringComparison.Ordinal) ? squadText[firstPrefix.Length..] : squadText;

        foreach (var section in normalized.Split(sectionSep, StringSplitOptions.RemoveEmptyEntries))
        {
            var nl = section.IndexOf('\n');
            if (nl < 0)
            {
                continue;
            }

            var name = section[..nl].Trim();
            var output = section[(nl + 1)..].TrimEnd();

            if (!string.IsNullOrEmpty(name))
            {
                cache[name] = output;
            }
        }
    }

    private static string ReconstructTranscript(IReadOnlyList<string> squadNames, Dictionary<string, string> cache)
    {
        var sb = new StringBuilder();
        foreach (var name in squadNames)
        {
            if (!cache.TryGetValue(name, out var output))
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append("\n\n");
            }

            sb.Append("### ").Append(name).Append('\n').Append(output);
        }

        return sb.ToString();
    }

    private static string BuildValidationInput(string originalInput, string attributedSquadText, IReadOnlyList<SquadMember>? roster)
    {
        var sb = new StringBuilder();
        sb.Append("Original Request: ").Append(originalInput).Append("\n\n");

        if (roster is { Count: > 0 })
        {
            sb.Append("Squad Roster - each agent's assigned role:\n");
            foreach (var member in roster)
            {
                sb.Append("- ").Append(member.Name).Append(": ").Append(Truncate(member.Role, RoleSummaryBudget)).Append('\n');
            }

            sb.Append('\n');
        }

        sb.Append("Squad Output - each section is labeled with the agent that produced it:\n");
        sb.Append(string.IsNullOrWhiteSpace(attributedSquadText) ? "(the squad produced no output)" : attributedSquadText);

        sb.Append("\n\nJudge the COMBINED squad output as a whole: does it correctly and completely answer the Original Request? Ignore process chatter, hand-off lines, and brevity - judge the substance, not the wording. PASS when each agent covered its assigned part and the combined result answers the request. Only FAIL for a real defect: a wrong, missing, hallucinated, or off-role contribution. If you FAIL, the CRITIQUE must name the specific defect and RESPONSIBLE_AGENTS must list ONLY the agent(s) whose own output must be redone, using their exact roster name; agents whose output was already correct will not be re-run, so do not name them.");

        return sb.ToString();
    }

    private async Task<string> RunValidatorAsync(
        WorkflowTrigger trigger,
        AIAgent validator,
        string validationInput,
        StreamLogAccumulator streamLog,
        CancellationToken ct)
    {
        var validatorSession = await validator.CreateSessionAsync(ct);

        return await AgentTurnStreamer.RunAsync(validator, validationInput, validatorSession, trigger, AgentRole.Validator, nats, streamLog, ct);
    }

    /// <summary>
    /// Builds a lean context for the next correction turn as a single user message: the original request, the
    /// reviewer critique, and a truncated copy of the last combined answer. Failed-iteration noise is dropped so
    /// the small local context window never forgets the original request as iterations grow, and a single user
    /// turn avoids the back-to-back same-role messages some local chat templates mishandle.
    /// </summary>
    private static List<ChatMessage> BuildCorrectiveContext(string originalInput, string combinedAnswer, string critique, int nextAttempt)
    {
        var previous = combinedAnswer.Length > SquadTextBudget ? string.Concat(combinedAnswer.AsSpan(0, SquadTextBudget), "…") : combinedAnswer;

        var corrective =
            $"Original Request:\n{originalInput}\n\n" +
            $"[REVISION REQUIRED - attempt {nextAttempt}]\n" +
            "A reviewer found problems with the squad's combined answer (shown below for reference only).\n\n" +
            $"Reviewer critique:\n{critique}\n\n" +
            $"Squad's previous combined answer (reference - do NOT reproduce it):\n{previous}\n\n" +
            "Revise ONLY your own contribution to fix what the critique attributes to you. Stay strictly in your role, " +
            "do not rewrite or restate the other agents' sections, and output only your corrected contribution - no " +
            "apologies, acknowledgements, or STATUS lines.";

        return [new ChatMessage(ChatRole.User, corrective)];
    }

    private async Task EmitStatusAsync(WorkflowTrigger trigger, string author, string text, StreamLogAccumulator streamLog, CancellationToken ct)
    {
        await nats.StreamAgentUpdateAsync(trigger.TriggerType, author, text, AgentUpdateTypes.Status, ct);
        streamLog.Add(author, text, AgentUpdateTypes.Status);
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var oneLine = text.ReplaceLineEndings(" ").Trim();
        return oneLine.Length > max ? string.Concat(oneLine.AsSpan(0, max), "…") : oneLine;
    }
}
