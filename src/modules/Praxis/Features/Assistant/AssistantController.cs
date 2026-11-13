using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Activity;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Titles;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Praxis.Features.Assistant;

/// <summary>
/// Streams an agent over Server-Sent Events: the conversation's agent (see <see cref="IConversationService.ResolveChatAgentAsync"/>)
/// or the general assistant. For a persisted conversation the server owns the history
/// (<see cref="ConversationChatHistoryProvider"/>); tool-approval pauses are resumed from a serialized
/// <see cref="AgentSession"/> in the approving request's own scope.
/// </summary>
[ApiController]
[Authorize]
[Route($"{PraxisRoutes.Prefix}/assistant")]
public sealed class AssistantController(
    IAgentFactory agentFactory,
    IPendingApprovalStore approvalStore,
    IPdpContextAccessor runContext,
    ICurrentUser currentUser,
    IPromptEnhancementService promptEnhancementService,
    IRegistrySettingsService registrySettingsService,
    ConversationChatHistoryProvider conversationHistory,
    IConversationService conversationService,
    ITitleGenerator titleGenerator,
    ILogger<AssistantController> logger) : ControllerBase
{
    private const string GenericStreamErrorMessage = "An unexpected error occurred while generating the response.";

    /// <summary>
    /// Sends a chat message to the assistant and streams the response (thoughts, tool calls, text, approval requests).
    /// With a <see cref="ChatRequest.ConversationId"/> only the latest user message is taken from the request - prior
    /// turns are loaded from the persisted conversation. If a tool call needs approval, the stream ends with an
    /// approval request; resume it via <c>chat/approve</c>.
    /// </summary>
    /// <param name="requestBody">The chat request.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Chat([FromBody] ChatRequest requestBody, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        var lastUserMessage = requestBody.Messages.LastOrDefault(m => IsUserRole(m.Role));

        // Ambient run context (AsyncLocal): set here, in the method that awaits the agent run, so it flows to every tool.
        runContext.OriginalUserPrompt = lastUserMessage?.Content ?? string.Empty;
        runContext.UserId = currentUser.UserId;
        runContext.CorrelationId = Request.Headers.TryGetValue(CommonHeaderNames.CorrelationId, out var header) ? header.ToString() : Guid.NewGuid().ToString();

        await using var sse = await SseStreamWriter.StartAsync(Response, logger, ct);

        // Started before enhancement and the run so it overlaps them; it only calls the model, never the DbContext.
        var pendingTitle = await StartTitleGenerationAsync(requestBody.ConversationId, lastUserMessage?.Content, ct);

        var enhancedPrompt = await TryEnhancePromptAsync(lastUserMessage?.Content, ct);
        if (enhancedPrompt is not null)
        {
            await sse.WriteUpdateAsync(AgentUpdateTypes.EnhancedPrompt, enhancedPrompt, ct);
        }

        var persisted = requestBody.ConversationId is not null;

        // The conversation fixes its agent; custom agents resolve for this user explicitly (a Sovereign bypasses the
        // query filter, and must not pick up another member's same-named agent).
        var agentName = await conversationService.ResolveChatAgentAsync(requestBody.ConversationId, currentUser.UserId, ct);
        var agent = await agentFactory.CreateAsync(agentName, overrideModel: requestBody.Model, actingUserId: currentUser.UserId, chatHistory: persisted ? conversationHistory : null, ct: ct);
        var session = await agent.CreateSessionAsync(ct);

        List<AiChatMessage> input;
        if (requestBody.ConversationId is { } conversationId)
        {
            conversationHistory.Attach(session, conversationId);
            input = lastUserMessage is null ? [] : [CreateUserTurn(lastUserMessage.Content, enhancedPrompt)];
        }
        else
        {
            // Ephemeral chat: there is no server-side transcript, so the client-held history is the context.
            input = MapClientHistory(requestBody.Messages, enhancedPrompt);
        }

        await RunStreamingAsync(agent, agentName, session, input, requestBody.ConversationId, requestBody.Model, sse, ct, pendingTitle);
    }

    /// <summary>
    /// Approves or rejects a pending tool call and streams the resumed response. Only the user who owns the paused run
    /// can resume it.
    /// </summary>
    /// <param name="requestBody">The approval decision.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("chat/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task ApproveToolCall([FromBody] ApproveToolCallRequest requestBody, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        if (!approvalStore.TryTake(requestBody.RequestId, currentUser.UserId, out var pending) || pending is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        runContext.OriginalUserPrompt = pending.OriginalUserPrompt;
        runContext.CorrelationId = pending.CorrelationId;
        runContext.UserId = pending.UserId;

        await using var sse = await SseStreamWriter.StartAsync(Response, logger, ct);

        var persisted = pending.ConversationId is not null;
        var agent = await agentFactory.CreateAsync(pending.AgentName, overrideModel: pending.Model, actingUserId: pending.UserId, chatHistory: persisted ? conversationHistory : null, ct: ct);
        var session = await agent.DeserializeSessionAsync(pending.Session, cancellationToken: ct);

        // The documented resume: send only the approval response; the session supplies the rest of the context.
        AiChatMessage approval = new(ChatRole.User, [pending.ApprovalRequest.CreateResponse(requestBody.Approved)]);

        await RunStreamingAsync(agent, pending.AgentName, session, [approval], pending.ConversationId, pending.Model, sse, ct);
    }

    private async Task RunStreamingAsync(
        AIAgent agent,
        string agentName,
        AgentSession session,
        IReadOnlyList<AiChatMessage> input,
        Guid? conversationId,
        string model,
        SseStreamWriter sse,
        CancellationToken ct,
        Task<string?>? pendingTitle = null)
    {
        var run = new AgentRunStream(sse);
        conversationHistory.TrackActivity(run.Activity);

        try
        {
            await run.RunAsync(agent.RunStreamingAsync(input, session, cancellationToken: ct), ct);

            await PublishTitleAsync(conversationId, pendingTitle, sse, ct);

            if (run.Contents.OfType<ToolApprovalRequestContent>().FirstOrDefault() is { } approvalRequest)
            {
                await PauseForApprovalAsync(agent, agentName, session, approvalRequest, conversationId, model, sse, ct);
                return;
            }

            await sse.CompleteAsync(ct);
        }
        catch (OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Streaming request cancelled. CorrelationId: {CorrelationId}", runContext.CorrelationId);
            }

            await PersistPartialResponseAsync(session, conversationId, run);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while streaming assistant response. CorrelationId: {CorrelationId}", runContext.CorrelationId);
            await sse.FailAsync(GenericStreamErrorMessage);
        }
    }

    private async Task PauseForApprovalAsync(
        AIAgent agent,
        string agentName,
        AgentSession session,
        ToolApprovalRequestContent request,
        Guid? conversationId,
        string model,
        SseStreamWriter sse,
        CancellationToken ct)
    {
        var serializedSession = await agent.SerializeSessionAsync(session, cancellationToken: ct);

        approvalStore.Add(request.RequestId, new PendingApproval(
            serializedSession,
            request,
            conversationId,
            model,
            runContext.UserId,
            runContext.CorrelationId,
            runContext.OriginalUserPrompt,
            agentName));

        var call = request.ToolCall as FunctionCallContent;
        var approvalData = new ToolApprovalData(request.RequestId, call?.Name ?? "unknown", call?.Arguments, call?.CallId);

        await sse.WriteUpdateAsync(AgentUpdateTypes.ApprovalRequest, JsonSerializer.Serialize(approvalData), ct);
    }

    private async Task PersistPartialResponseAsync(AgentSession session, Guid? conversationId, AgentRunStream run)
    {
        var text = run.Text;
        if (conversationId is null || text.Length == 0)
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
            logger.LogWarning(ex, "Failed to persist partial assistant response after cancellation. CorrelationId: {CorrelationId}", runContext.CorrelationId);
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
    private async Task PublishTitleAsync(Guid? conversationId, Task<string?>? pendingTitle, SseStreamWriter sse, CancellationToken ct)
    {
        if (pendingTitle is null || conversationId is not { } id)
        {
            return;
        }

        try
        {
            if (await pendingTitle is { } title && await conversationService.TrySetGeneratedTitleAsync(id, title, ct))
            {
                await sse.WriteUpdateAsync(AgentUpdateTypes.Title, title, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // The client went away after the answer completed: the title is either stored (and shown on the next list
            // refresh) or the conversation stays pending for the next turn.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to store the generated title of conversation {ConversationId}.", id);
        }
    }

    private async Task<string?> TryEnhancePromptAsync(string? prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var settings = await registrySettingsService.GetAsync(currentUser.UserId, ct);
        if (!settings.Harness.IsPromptEnhancementEnabled)
        {
            return null;
        }

        var enhanced = await promptEnhancementService.EnhanceAsync(prompt, ct);

        return !string.IsNullOrWhiteSpace(enhanced) && !string.Equals(enhanced, prompt, StringComparison.Ordinal) ? enhanced : null;
    }

    /// <summary>
    /// The user turn sent to the model: the enhanced prompt when there is one, carrying the original text so the
    /// transcript stores what the user actually typed.
    /// </summary>
    private static AiChatMessage CreateUserTurn(string original, string? enhancedPrompt)
    {
        if (enhancedPrompt is null)
        {
            return new AiChatMessage(ChatRole.User, original);
        }

        return new AiChatMessage(ChatRole.User, enhancedPrompt)
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { [ConversationChatHistoryProvider.OriginalPromptProperty] = original },
        };
    }

    private static List<AiChatMessage> MapClientHistory(IReadOnlyList<ChatMessage> messages, string? enhancedPrompt)
    {
        var mapped = messages
            .Select(m => new AiChatMessage(IsUserRole(m.Role) ? ChatRole.User : ChatRole.Assistant, m.Content))
            .ToList();

        if (enhancedPrompt is not null && mapped.FindLastIndex(m => m.Role == ChatRole.User) is var last and >= 0)
        {
            mapped[last] = CreateUserTurn(mapped[last].Text, enhancedPrompt);
        }

        return mapped;
    }

    private static bool IsUserRole(string role) => role.Equals(ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase);
}
