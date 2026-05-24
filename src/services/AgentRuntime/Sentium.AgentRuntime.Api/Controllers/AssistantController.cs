using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Sentium.AgentRuntime.Infrastructure.Sentinel;
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

[ApiController]
[Authorize]
[Route("assistant")]
public sealed class AssistantController(
    IAgentFactory agentFactory,
    IPendingApprovalStore approvalStore,
    IPdpContextAccessor pdpContext,
    ILogger<AssistantController> logger) : ControllerBase
{
    private static readonly byte[] DataPrefix = "data: "u8.ToArray();
    private static readonly byte[] SseDelimiter = "\n\n"u8.ToArray();

    [HttpPost("chat")]
    public async Task Chat([FromBody] ChatRequest requestBody, [FromServices] IServiceScopeFactory scopeFactory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentNullException.ThrowIfNull(scopeFactory);

        ConfigureSseResponse();

        var lastUserMessage = requestBody.Messages.LastOrDefault(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));

        pdpContext.OriginalUserPrompt = lastUserMessage?.Content ?? string.Empty;

        pdpContext.CorrelationId = Request.Headers.TryGetValue(HeaderNames.CorrelationId, out var hdr)
            ? hdr.ToString()
            : Guid.NewGuid().ToString();

        if (requestBody.ConversationId.HasValue && lastUserMessage is not null)
        {
            using var scope = scopeFactory.CreateScope();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
            await conversationService.AddMessageAsync(requestBody.ConversationId.Value, "user", lastUserMessage.Content, ct: ct);
        }

        var agent = await agentFactory.CreateAsync(AgentRole.GeneralAssistant, overrideModel: requestBody.Model, ct: ct);
        var session = await agent.CreateSessionAsync(ct);

        var chatMessages = requestBody.Messages.Select(m => new ChatMessage(
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
            m.Content
        )).ToList();

        await RunStreamingAsync(agent, session, chatMessages, requestBody.ConversationId, requestBody.Model, scopeFactory, ct);
    }

    [HttpPost("chat/approve")]
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

        var approvalResponseMessage = new ChatMessage(ChatRole.User, [pending.ApprovalRequest.CreateResponse(requestBody.Approved)]);

        var restoredHistory = new List<ChatMessage>(pending.ChatHistory)
        {
            approvalResponseMessage
        };

        await RunStreamingAsync(pending.Agent, pending.Session, restoredHistory, pending.ConversationId, pending.Model, scopeFactory, ct);
    }

    private void ConfigureSseResponse()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers.Append("X-Accel-Buffering", "no");
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
                        CorrelationId: pdpContext.CorrelationId
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
                    "assistant",
                    assistantResponse.ToString(),
                    thoughtBuilder.Length > 0 ? thoughtBuilder.ToString() : null,
                    toolCallLog.Count > 0 ? toolCallLog : null,
                    ct);
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
}
