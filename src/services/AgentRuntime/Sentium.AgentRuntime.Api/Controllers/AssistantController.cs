using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Conversations;
using Sentium.AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sentium.AgentRuntime.Api.Controllers;

[ApiController]
[Authorize]
[Route("assistant")]
public sealed class AssistantController(
    IAgentFactory agentFactory,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private static readonly Uri OllamaBase = new("http://localhost:11434");

    [HttpGet("models")]
    public async Task<IActionResult> GetModels(CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient("ollama");
        using var response = await client.GetAsync(new Uri(OllamaBase, "/api/tags"), ct);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonNode.Parse(body);
        var models = doc?["models"]?.AsArray()
            .Select(m => m?["name"]?.GetValue<string>() ?? string.Empty)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList() ?? [];

        return Ok(models);
    }

    [HttpPost("chat")]
    public async Task Chat([FromBody] ChatRequest requestBody, [FromServices] IServiceScopeFactory scopeFactory, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        if (requestBody.ConversationId.HasValue && requestBody.Messages.Count > 0)
        {
            var lastMsg = requestBody.Messages[requestBody.Messages.Count - 1];
            if (lastMsg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = scopeFactory.CreateScope();
                var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
                await conversationService.AddMessageAsync(requestBody.ConversationId.Value, "user", lastMsg.Content, ct: ct);
            }
        }

        var agent = await agentFactory.CreateAsync(AgentRole.GeneralAssistant, ct: ct);
        var session = await agent.CreateSessionAsync(ct);

        var chatMessages = requestBody.Messages.Select(m => new Microsoft.Extensions.AI.ChatMessage(
            m.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
            m.Content
        )).ToList();

        Response.ContentType = "text/event-stream";
        var assistantResponse = new StringBuilder();
        var thoughtBuilder = new StringBuilder();
        var toolCallLog = new List<string>();

        var responseStream = agent.RunStreamingAsync(chatMessages, session, cancellationToken: ct);

        await foreach (var update in responseStream)
        {
            if (update.Contents.OfType<TextReasoningContent>().FirstOrDefault() is { } reasoning)
            {
                thoughtBuilder.Append(reasoning.Text);
                await SendUiUpdate("thought", reasoning.Text, ct);
            }

            if (update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is { } call)
            {
                var toolLabel = $"Calling {call.Name}...";
                toolCallLog.Add(toolLabel);
                await SendUiUpdate("tool", toolLabel, ct);
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                assistantResponse.Append(update.Text);
                await SendUiUpdate("message", update.Text, ct);
            }
        }

        if (requestBody.ConversationId.HasValue && assistantResponse.Length > 0)
        {
            using var scope = scopeFactory.CreateScope();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
            await conversationService.AddMessageAsync(
                requestBody.ConversationId.Value,
                "assistant",
                assistantResponse.ToString(),
                thoughtBuilder.Length > 0 ? thoughtBuilder.ToString() : null,
                toolCallLog.Count > 0 ? toolCallLog : null,
                ct);
        }
    }

    private async Task SendUiUpdate(string type, string content, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new
        {
            type,
            message = new { content },
            done = false
        });
        await Response.WriteAsync(json + "\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
