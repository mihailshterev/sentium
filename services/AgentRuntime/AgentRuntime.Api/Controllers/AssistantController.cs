using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Conversations;
using AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Authorize]
[Route("assistant")]
public sealed class AssistantController(
    IAgentFactory agentFactory,
    IHttpClientFactory httpClientFactory,
    IConversationService conversationService) : ControllerBase
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
    public async Task Chat([FromBody] ChatRequest requestBody, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestBody);

        if (requestBody.Messages is { Count: > 0 })
        {
            var lastIndex = requestBody.Messages.Count - 1;
            var lastUserMessage = requestBody.Messages[lastIndex];

            if (requestBody.ConversationId.HasValue && string.Equals(lastUserMessage.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                await conversationService.AddMessageAsync(requestBody.ConversationId.Value, "user", lastUserMessage.Content, ct);
            }
        }

        var agent = await agentFactory.CreateAsync(AgentRole.GeneralAssistant, ct: ct);

        Response.ContentType = "text/event-stream";
        var assistantResponse = new StringBuilder();

        var userContent = requestBody.Messages[requestBody.Messages.Count - 1].Content;
        var responseStream = agent.RunStreamingAsync(userContent, cancellationToken: ct);

        await foreach (var update in responseStream)
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                assistantResponse.Append(update.Text);

                var jsonChunk = $"{{\"message\":{{\"content\":\"{JsonEncodedText.Encode(update.Text)}\"}},\"done\":false}}";
                await Response.WriteAsync(jsonChunk + "\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }

        if (requestBody.ConversationId.HasValue && assistantResponse.Length > 0)
        {
            await conversationService.AddMessageAsync(requestBody.ConversationId.Value, "assistant", assistantResponse.ToString(), ct);
        }
    }
}
