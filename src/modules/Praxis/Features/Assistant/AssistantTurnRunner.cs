using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Activity;
using Sentium.Praxis.Features.Agents.Approvals;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Titles;
using Sentium.Praxis.Features.Workspaces;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// Runs one <see cref="AssistantTurn"/> in a scope acting as its user: the conversation's agent (see
/// <see cref="IConversationService.ResolveChatAgentAsync"/>) or the general assistant. For a persisted conversation the
/// server owns the history (<see cref="ConversationChatHistoryProvider"/>). A tool call that needs approval pauses the
/// run: its <see cref="AgentSession"/> is serialized into <see cref="IPendingApprovalStore"/> and the turn ends with an
/// approval request, resumed by a later turn carrying the decision.
/// </summary>
internal sealed class AssistantTurnRunner(
    IAgentFactory agentFactory,
    IPendingApprovalStore approvalStore,
    IPromptEnhancementService promptEnhancementService,
    IRegistrySettingsService registrySettingsService,
    ConversationChatHistoryProvider conversationHistory,
    IConversationService conversationService,
    ITitleGenerator titleGenerator,
    ILogger<AssistantTurnRunner> logger)
{
    /// <summary>
    /// Streams the turn's updates to <paramref name="sink"/>; the caller writes the terminal frame. When
    /// <paramref name="ct"/> fires, the partial answer of a persisted conversation is saved and the
    /// <see cref="OperationCanceledException"/> rethrown.
    /// </summary>
    public Task RunAsync(AssistantTurn turn, IAgentUpdateSink sink, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(sink);

        return turn.Approval is { } approval ? ResumeAsync(turn, approval, sink, ct) : ChatAsync(turn, sink, ct);
    }

    private async Task ChatAsync(AssistantTurn turn, IAgentUpdateSink sink, CancellationToken ct)
    {
        // Started before enhancement and the run so it overlaps them; it only calls the model, never the DbContext.
        var pendingTitle = await StartTitleGenerationAsync(turn.ConversationId, turn.Prompt, ct);

        var enhancedPrompt = await TryEnhancePromptAsync(turn.UserId, turn.Prompt, ct);
        if (enhancedPrompt is not null)
        {
            await sink.WriteUpdateAsync(AgentUpdateTypes.EnhancedPrompt, enhancedPrompt, ct);
        }

        var persisted = turn.ConversationId is not null;

        // The conversation fixes its agent; custom agents resolve for this user explicitly.
        var agentName = await conversationService.ResolveChatAgentAsync(turn.ConversationId, turn.UserId, ct);
        var agent = await agentFactory.CreateAsync(agentName, overrideModel: turn.Model, actingUserId: turn.UserId, chatHistory: persisted ? conversationHistory : null, ct: ct);
        var session = await agent.CreateSessionAsync(ct);

        List<AiChatMessage> input;
        Guid? workspaceId;
        if (turn.ConversationId is { } conversationId)
        {
            conversationHistory.Attach(session, conversationId);
            input = turn.Prompt is null ? [] : [CreateUserTurn(turn.Prompt, enhancedPrompt, turn.Attachments)];
            workspaceId = await conversationService.GetWorkspaceIdAsync(conversationId, turn.UserId, ct);
        }
        else
        {
            // Ephemeral chat: there is no server-side transcript, so the client-held history is the context.
            input = MapClientHistory(turn.Messages, enhancedPrompt, turn.Prompt);
            workspaceId = turn.WorkspaceId;
        }

        WorkspaceContextProvider.Attach(session, workspaceId, [.. turn.Attachments.Select(a => a.FileId)]);

        await RunStreamingAsync(agent, agentName, session, input, turn, sink, ct, pendingTitle);
    }

    private async Task ResumeAsync(AssistantTurn turn, ApprovalDecision decision, IAgentUpdateSink sink, CancellationToken ct)
    {
        var pending = decision.Pending;
        var persisted = pending.ConversationId is not null;
        var agent = await agentFactory.CreateAsync(pending.AgentName, overrideModel: pending.Model, actingUserId: turn.UserId, chatHistory: persisted ? conversationHistory : null, ct: ct);
        var session = await agent.DeserializeSessionAsync(pending.Session, cancellationToken: ct);

        // The documented resume: send only the approval response; the session supplies the rest of the context.
        AiChatMessage approval = new(ChatRole.User, [pending.ApprovalRequest.CreateResponse(decision.Approved)]);

        await RunStreamingAsync(agent, pending.AgentName, session, [approval], turn, sink, ct);
    }

    private async Task RunStreamingAsync(
        AIAgent agent,
        string agentName,
        AgentSession session,
        IReadOnlyList<AiChatMessage> input,
        AssistantTurn turn,
        IAgentUpdateSink sink,
        CancellationToken ct,
        Task<string?>? pendingTitle = null)
    {
        var run = new AgentRunStream(sink);
        conversationHistory.TrackActivity(run.Activity);

        try
        {
            await run.RunAsync(agent.RunStreamingAsync(input, session, cancellationToken: ct), ct);

            await PublishTitleAsync(turn.ConversationId, pendingTitle, sink, ct);

            if (run.Contents.OfType<ToolApprovalRequestContent>().FirstOrDefault() is { } approvalRequest)
            {
                await PauseForApprovalAsync(agent, agentName, session, approvalRequest, turn, sink, ct);
            }
        }
        catch (OperationCanceledException)
        {
            await PersistPartialResponseAsync(session, turn, run);
            throw;
        }
    }

    private async Task PauseForApprovalAsync(
        AIAgent agent,
        string agentName,
        AgentSession session,
        ToolApprovalRequestContent request,
        AssistantTurn turn,
        IAgentUpdateSink sink,
        CancellationToken ct)
    {
        var serializedSession = await agent.SerializeSessionAsync(session, cancellationToken: ct);

        approvalStore.Add(request.RequestId, new PendingApproval(
            serializedSession,
            request,
            turn.ConversationId,
            turn.Model,
            turn.UserId,
            turn.CorrelationId,
            turn.OriginalUserPrompt,
            agentName));

        var call = request.ToolCall as FunctionCallContent;
        var approvalData = new ToolApprovalData(request.RequestId, call?.Name ?? "unknown", call?.Arguments, call?.CallId);

        await sink.WriteUpdateAsync(AgentUpdateTypes.ApprovalRequest, JsonSerializer.Serialize(approvalData), ct);
    }

    private async Task PersistPartialResponseAsync(AgentSession session, AssistantTurn turn, AgentRunStream run)
    {
        var text = run.Text;
        if (turn.ConversationId is null || text.Length == 0)
        {
            return;
        }

        try
        {
            var (answer, steps) = AgentTurnSteps.Build([new AiChatMessage(ChatRole.Assistant, [.. run.Contents])], run.Activity.Durations);
            await conversationHistory.PersistPartialResponseAsync(session, answer.Length > 0 ? answer : text, steps.Count > 0 ? steps : null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist partial assistant response after cancellation. CorrelationId: {CorrelationId}", turn.CorrelationId);
        }
    }

    /// <summary>
    /// Starts generating a title when this turn belongs to a conversation that is still untitled; otherwise
    /// <see langword="null"/>. The returned task never faults (see <see cref="ITitleGenerator.GenerateAsync"/>).
    /// </summary>
    private async Task<Task<string?>?> StartTitleGenerationAsync(Guid? conversationId, string? prompt, CancellationToken ct)
    {
        if (conversationId is not { } id || string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        try
        {
            return await conversationService.IsTitlePendingAsync(id, ct) ? titleGenerator.GenerateAsync(prompt, ct) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not check whether conversation {ConversationId} needs a title.", id);
            return null;
        }
    }

    /// <summary>
    /// Stores the generated title (unless the user renamed the conversation meanwhile) and tells the client. Best effort:
    /// a failure leaves the conversation pending, so the next turn tries again.
    /// </summary>
    private async Task PublishTitleAsync(Guid? conversationId, Task<string?>? pendingTitle, IAgentUpdateSink sink, CancellationToken ct)
    {
        if (pendingTitle is null || conversationId is not { } id)
        {
            return;
        }

        try
        {
            if (await pendingTitle is { } title && await conversationService.TrySetGeneratedTitleAsync(id, title, ct))
            {
                await sink.WriteUpdateAsync(AgentUpdateTypes.Title, title, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to store the generated title of conversation {ConversationId}.", id);
        }
    }

    private async Task<string?> TryEnhancePromptAsync(Guid userId, string? prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var settings = await registrySettingsService.GetAsync(userId, ct);
        if (!settings.Harness.IsPromptEnhancementEnabled)
        {
            return null;
        }

        var enhanced = await promptEnhancementService.EnhanceAsync(prompt, ct);

        return !string.IsNullOrWhiteSpace(enhanced) && !string.Equals(enhanced, prompt, StringComparison.Ordinal) ? enhanced : null;
    }

    /// <summary>
    /// The user turn sent to the model: the enhanced prompt when there is one, carrying the original text so the
    /// transcript stores what the user actually typed, and the attached files so the transcript lists them.
    /// </summary>
    private static AiChatMessage CreateUserTurn(string original, string? enhancedPrompt, IReadOnlyList<MessageAttachment>? attachments = null)
    {
        var properties = new AdditionalPropertiesDictionary();
        if (enhancedPrompt is not null)
        {
            properties[ConversationChatHistoryProvider.OriginalPromptProperty] = original;
        }

        if (attachments is { Count: > 0 })
        {
            ConversationChatHistoryProvider.WithAttachments(properties, attachments);
        }

        return new AiChatMessage(ChatRole.User, enhancedPrompt ?? original)
        {
            AdditionalProperties = properties.Count > 0 ? properties : null,
        };
    }

    private static List<AiChatMessage> MapClientHistory(IReadOnlyList<ChatMessage> messages, string? enhancedPrompt, string? prompt)
    {
        var mapped = messages
            .Select(m => new AiChatMessage(ChatRoles.IsUser(m.Role) ? ChatRole.User : ChatRole.Assistant, m.Content))
            .ToList();

        if (prompt is not null && mapped.FindLastIndex(m => m.Role == ChatRole.User) is var last and >= 0)
        {
            mapped[last] = CreateUserTurn(prompt, enhancedPrompt);
        }

        return mapped;
    }
}
