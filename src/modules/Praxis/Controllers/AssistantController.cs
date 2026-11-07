using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Security;
using Sentium.Praxis.Common.Streaming;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Conversations;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Sentinel;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Praxis.Controllers;

/// <summary>
/// Streams the general assistant over Server-Sent Events. For a persisted conversation the server owns the history
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

        var enhancedPrompt = await TryEnhancePromptAsync(lastUserMessage?.Content, ct);
        if (enhancedPrompt is not null)
        {
            await sse.SendUpdateAsync(AgentUpdateTypes.EnhancedPrompt, enhancedPrompt, ct);
        }

        var persisted = requestBody.ConversationId is not null;
        var agent = await agentFactory.CreateAsync(AgentRole.GeneralAssistant, overrideModel: requestBody.Model, chatHistory: persisted ? conversationHistory : null, ct: ct);
        var session = await agent.CreateSessionAsync(ct);

        List<ChatMessage> input;
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

        await RunStreamingAsync(agent, session, input, requestBody.ConversationId, requestBody.Model, sse, ct);
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
        var agent = await agentFactory.CreateAsync(AgentRole.GeneralAssistant, overrideModel: pending.Model, chatHistory: persisted ? conversationHistory : null, ct: ct);
        var session = await agent.DeserializeSessionAsync(pending.Session, cancellationToken: ct);

        // The documented resume: send only the approval response; the session supplies the rest of the context.
        ChatMessage approval = new(ChatRole.User, [pending.ApprovalRequest.CreateResponse(requestBody.Approved)]);

        await RunStreamingAsync(agent, session, [approval], pending.ConversationId, pending.Model, sse, ct);
    }

    private async Task RunStreamingAsync(
        AIAgent agent,
        AgentSession session,
        IReadOnlyList<ChatMessage> input,
        Guid? conversationId,
        string model,
        SseStreamWriter sse,
        CancellationToken ct)
    {
        var text = new StringBuilder();
        var thought = new StringBuilder();
        var toolCalls = new List<string>();
        ToolApprovalRequestContent? approvalRequest = null;

        try
        {
            await foreach (var update in agent.RunStreamingAsync(input, session, cancellationToken: ct))
            {
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case TextReasoningContent reasoning when !string.IsNullOrEmpty(reasoning.Text):
                            thought.Append(reasoning.Text);
                            await sse.SendUpdateAsync(AgentUpdateTypes.Thought, reasoning.Text, ct);
                            break;
                        case ToolApprovalRequestContent request:
                            approvalRequest ??= request;
                            break;
                        case FunctionCallContent call:
                            var label = ConversationChatHistoryProvider.ToolCallLabel(call.Name);
                            toolCalls.Add(label);
                            await sse.SendUpdateAsync(AgentUpdateTypes.Tool, label, ct);
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(update.Text))
                {
                    text.Append(update.Text);
                    await sse.SendUpdateAsync(AgentUpdateTypes.Message, update.Text, ct);
                }
            }

            if (approvalRequest is not null)
            {
                await PauseForApprovalAsync(agent, session, approvalRequest, conversationId, model, sse, ct);
                return;
            }

            await sse.SendFrameAsync(new StreamDoneFrame(AgentUpdateTypes.Done), ct);
        }
        catch (OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Streaming request cancelled. CorrelationId: {CorrelationId}", runContext.CorrelationId);
            }

            await PersistPartialResponseAsync(session, conversationId, text, thought, toolCalls);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while streaming assistant response. CorrelationId: {CorrelationId}", runContext.CorrelationId);

            try
            {
                await sse.SendFrameAsync(new StreamErrorFrame(GenericStreamErrorMessage), CancellationToken.None);
            }
            catch (Exception writeEx)
            {
                logger.LogWarning(writeEx, "Failed to write SSE error response.");
            }
        }
    }

    private async Task PauseForApprovalAsync(
        AIAgent agent,
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
            runContext.OriginalUserPrompt));

        var call = request.ToolCall as FunctionCallContent;
        var approvalData = new ToolApprovalData(request.RequestId, call?.Name ?? "unknown", call?.Arguments);

        await sse.SendUpdateAsync(AgentUpdateTypes.ApprovalRequest, JsonSerializer.Serialize(approvalData), ct);
    }

    private async Task PersistPartialResponseAsync(AgentSession session, Guid? conversationId, StringBuilder text, StringBuilder thought, List<string> toolCalls)
    {
        if (conversationId is null || text.Length == 0)
        {
            return;
        }

        try
        {
            await conversationHistory.PersistPartialResponseAsync(
                session,
                text.ToString(),
                thought.Length > 0 ? thought.ToString() : null,
                toolCalls.Count > 0 ? toolCalls : null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist partial assistant response after cancellation. CorrelationId: {CorrelationId}", runContext.CorrelationId);
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
    private static ChatMessage CreateUserTurn(string original, string? enhancedPrompt)
    {
        if (enhancedPrompt is null)
        {
            return new ChatMessage(ChatRole.User, original);
        }

        return new ChatMessage(ChatRole.User, enhancedPrompt)
        {
            AdditionalProperties = new AdditionalPropertiesDictionary { [ConversationChatHistoryProvider.OriginalPromptProperty] = original },
        };
    }

    private static List<ChatMessage> MapClientHistory(IReadOnlyList<Dtos.ChatMessage> messages, string? enhancedPrompt)
    {
        var mapped = messages
            .Select(m => new ChatMessage(IsUserRole(m.Role) ? ChatRole.User : ChatRole.Assistant, m.Content))
            .ToList();

        if (enhancedPrompt is not null && mapped.FindLastIndex(m => m.Role == ChatRole.User) is var last and >= 0)
        {
            mapped[last] = CreateUserTurn(mapped[last].Text, enhancedPrompt);
        }

        return mapped;
    }

    private static bool IsUserRole(string role) => role.Equals(ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase);
}
