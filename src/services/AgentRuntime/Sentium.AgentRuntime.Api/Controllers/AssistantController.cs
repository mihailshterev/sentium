using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
using Sentium.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using System.Text;
using System.Text.Json;
using Sentium.Shared.Constants;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using System.Buffers;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Controller for handling interactions with the general assistant agent.
/// </summary>
[ApiController]
[Authorize]
[Route("assistant")]
public sealed class AssistantController(
    IAgentFactory agentFactory,
    IPendingApprovalStore approvalStore,
    IPdpContextAccessor pdpContext,
    ICurrentUser currentUser,
    IPromptEnhancementService promptEnhancementService,
    IRegistrySettingsService registrySettingsService,
    ILogger<AssistantController> logger) : ControllerBase
{
    private static readonly byte[] DataPrefix = "data: "u8.ToArray();
    private static readonly byte[] SseDelimiter = "\n\n"u8.ToArray();

    /// <summary>
    /// Handles a chat message to the assistant agent and streams back responses using Server-Sent Events (SSE).
    /// The request can include an optional conversation ID to link messages to an existing conversation.
    /// If the agent triggers a tool call that requires approval, the stream will send an approval request message and pause
    /// until approval is granted via the /assistant/chat/approve endpoint.
    /// </summary>
    /// <param name="requestBody">The chat request containing messages and optional conversation ID.</param>
    /// <param name="scopeFactory">Service scope factory for creating scoped services during the request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A streaming response with assistant messages, thoughts, tool call logs, and approval requests.</returns>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Chat([FromBody] ChatRequest requestBody, [FromServices] IServiceScopeFactory scopeFactory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        ConfigureSseResponse();

        var lastUserMessage = requestBody.Messages.LastOrDefault(m => m.Role.Equals(ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase));

        pdpContext.OriginalUserPrompt = lastUserMessage?.Content ?? string.Empty;
        pdpContext.UserId = currentUser.UserId;

        pdpContext.CorrelationId = Request.Headers.TryGetValue(CommonHeaderNames.CorrelationId, out var hdr)
            ? hdr.ToString()
            : Guid.NewGuid().ToString();

        var enhancedPrompt = await TryEnhancePromptAsync(lastUserMessage?.Content, ct);

        if (requestBody.ConversationId.HasValue && lastUserMessage is not null)
        {
            using var scope = scopeFactory.CreateScope();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
            await conversationService.AddMessageAsync(requestBody.ConversationId.Value, ChatRole.User.ToString(), lastUserMessage.Content, enhancedPrompt: enhancedPrompt, ct: ct);
        }

        if (enhancedPrompt is not null)
        {
            await SendUiUpdate(AgentUpdateTypes.EnhancedPrompt, enhancedPrompt, ct);
        }

        var agent = await agentFactory.CreateAsync(AgentRole.GeneralAssistant, overrideModel: requestBody.Model, ct: ct);
        var session = await agent.CreateSessionAsync(ct);

        var chatMessages = requestBody.Messages.Select(m => new ChatMessage(
            m.Role.Equals(ChatRole.User.ToString(), StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
            m.Content
        )).ToList();

        if (enhancedPrompt is not null)
        {
            for (var i = chatMessages.Count - 1; i >= 0; i--)
            {
                if (chatMessages[i].Role == ChatRole.User)
                {
                    chatMessages[i] = new ChatMessage(ChatRole.User, enhancedPrompt);
                    break;
                }
            }
        }

        await RunStreamingAsync(agent, session, chatMessages, requestBody.ConversationId, requestBody.Model, scopeFactory, ct);
    }

    /// <summary>
    /// Handles approval of a pending tool call that was triggered during an assistant chat session.
    /// This endpoint is called by the client when the user approves or rejects a tool call.
    /// </summary>
    /// <param name="requestBody">The approval request containing the request ID and approval decision.</param>
    /// <param name="scopeFactory">Service scope factory for creating scoped services during the request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A streaming response with assistant messages, thoughts, tool call logs, and approval requests.</returns>
    [HttpPost("chat/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task ApproveToolCall([FromBody] ApproveToolCallRequest requestBody, [FromServices] IServiceScopeFactory scopeFactory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        if (!approvalStore.TryTake(requestBody.RequestId, out var pending) || pending is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        ConfigureSseResponse();

        pdpContext.OriginalUserPrompt = pending.OriginalUserPrompt;
        pdpContext.CorrelationId = pending.CorrelationId;
        pdpContext.UserId = pending.UserId;
        pdpContext.AgentName = pending.AgentName;

        var approvalResponseMessage = new ChatMessage(ChatRole.User, [pending.ApprovalRequest.CreateResponse(requestBody.Approved)]);

        var restoredHistory = new List<ChatMessage>(pending.ChatHistory)
        {
            approvalResponseMessage
        };

        await RunStreamingAsync(pending.Agent, pending.Session, restoredHistory, pending.ConversationId, pending.Model, scopeFactory, ct);
    }

    private async Task RunStreamingAsync(
        AIAgent agent,
        AgentSession session,
        IEnumerable<ChatMessage> messages,
        Guid? conversationId,
        string model,
        IServiceScopeFactory scopeFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        var assistantResponse = new StringBuilder();
        var thoughtBuilder = new StringBuilder();
        var toolCallLog = new List<string>();

        try
        {
            var responseStream = agent.RunStreamingAsync(messages, session, cancellationToken: ct);

            await foreach (var update in responseStream)
            {
                if (update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning)
                {
                    thoughtBuilder.Append(reasoning.Text);
                    await SendUiUpdate(AgentUpdateTypes.Thought, reasoning.Text, ct);
                }

                if (update.Contents.OfType<ToolApprovalRequestContent>().FirstOrDefault() is { } approvalRequest)
                {
                    var chatHistorySnapshot = messages.ToList();

                    var approval = new PendingApproval(
                        agent,
                        session,
                        approvalRequest,
                        conversationId,
                        model,
                        chatHistorySnapshot,
                        OriginalUserPrompt: pdpContext.OriginalUserPrompt,
                        CorrelationId: pdpContext.CorrelationId,
                        UserId: pdpContext.UserId,
                        AgentName: pdpContext.AgentName
                    );

                    approvalStore.Add(approvalRequest.RequestId, approval);

                    var toolCall = approvalRequest.ToolCall as FunctionCallContent;

                    var approvalData = new
                    {
                        requestId = approvalRequest.RequestId,
                        toolName = toolCall?.Name ?? "unknown",
                        arguments = toolCall?.Arguments,
                    };

                    await SendUiUpdate(AgentUpdateTypes.ApprovalRequest, JsonSerializer.Serialize(approvalData), ct);
                    return;
                }

                if (update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is { } call)
                {
                    var toolLabel = $"Calling {call.Name}...";
                    toolCallLog.Add(toolLabel);
                    await SendUiUpdate(AgentUpdateTypes.Tool, toolLabel, ct);
                }

                if (!string.IsNullOrEmpty(update.Text))
                {
                    assistantResponse.Append(update.Text);
                    await SendUiUpdate(AgentUpdateTypes.Message, update.Text, ct);
                }
            }

            if (conversationId.HasValue && assistantResponse.Length > 0)
            {
                using var scope = scopeFactory.CreateScope();
                var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();

                await conversationService.AddMessageAsync(
                    conversationId.Value,
                    ChatRole.Assistant.ToString(),
                    assistantResponse.ToString(),
                    thought: thoughtBuilder.Length > 0 ? thoughtBuilder.ToString() : null,
                    toolCalls: toolCallLog.Count > 0 ? toolCallLog : null,
                    ct: ct
                );
            }

            var completeJson = JsonSerializer.Serialize(new { type = "done", done = true });
            await Response.WriteAsync($"data: {completeJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Streaming request cancelled. CorrelationId: {CorrelationId}", pdpContext.CorrelationId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while streaming assistant response. CorrelationId: {CorrelationId}", pdpContext.CorrelationId);

            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
            }

            try
            {
                var errorJson = JsonSerializer.Serialize(new { type = "error", message = ex.Message, done = true });
                await Response.WriteAsync($"data: {errorJson}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            catch (Exception writeEx)
            {
                logger.LogWarning(writeEx, "Failed to write SSE error response.");
            }
        }
    }

    private async Task<string?> TryEnhancePromptAsync(string? prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return null;
        }

        var settings = await registrySettingsService.GetAsync(ct);
        if (!settings.Harness.IsPromptEnhancementEnabled)
        {
            return null;
        }

        var enhanced = await promptEnhancementService.EnhanceAsync(prompt, ct);

        return !string.IsNullOrWhiteSpace(enhanced) && !string.Equals(enhanced, prompt, StringComparison.Ordinal) ? enhanced : null;
    }

    private async Task SendUiUpdate(string type, string content, CancellationToken ct)
    {
        var pipeWriter = Response.BodyWriter;

        pipeWriter.Write(DataPrefix);

        await using var jsonWriter = new Utf8JsonWriter(pipeWriter);

        jsonWriter.WriteStartObject();
        jsonWriter.WriteString("type", type);

        jsonWriter.WriteStartObject("message");
        jsonWriter.WriteString("content", content);
        jsonWriter.WriteEndObject();

        jsonWriter.WriteBoolean("done", false);

        jsonWriter.WriteEndObject();

        await jsonWriter.FlushAsync(ct);

        pipeWriter.Write(SseDelimiter);

        await pipeWriter.FlushAsync(ct);
    }

    private void ConfigureSseResponse()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append(CommonHeaderNames.AccelBuffering, "no");
    }
}
