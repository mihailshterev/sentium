using AgentRuntime.Core.Conversations;
using AgentRuntime.Core.Dtos;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentRuntime.Api.Controllers;

[ApiController]
[Route("assistant")]
public sealed class AssistantController(IHttpClientFactory httpClientFactory, IConversationService conversationService) : ControllerBase
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

        if (requestBody.ConversationId.HasValue && requestBody.Messages.Count > 0)
        {
            var lastUser = requestBody.Messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (lastUser is not null)
            {
                await conversationService.AddMessageAsync(requestBody.ConversationId.Value, "user", lastUser.Content, ct);
            }
        }

        using var client = httpClientFactory.CreateClient("ollama");

        var ollamaPayload = new
        {
            model = requestBody.Model,
            messages = requestBody.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content,
                images = m.Images ?? []
            }),
            stream = true
        };

        var json = JsonSerializer.Serialize(ollamaPayload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(OllamaBase, "/api/chat"))
        {
            Content = content
        };

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        Response.ContentType = "application/json";
        Response.StatusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            await response.Content.CopyToAsync(Response.Body, ct);
            return;
        }

        var assistantContent = new StringBuilder();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await Response.WriteAsync(line + "\n", ct);
            await Response.Body.FlushAsync(ct);

            try
            {
                var parsed = JsonNode.Parse(line);
                var token = parsed?["message"]?["content"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(token))
                {
                    assistantContent.Append(token);
                }
            }
            catch { /* non-JSON line — skip */ }
        }

        if (requestBody.ConversationId.HasValue && assistantContent.Length > 0)
        {
            await conversationService.AddMessageAsync(requestBody.ConversationId.Value, "assistant", assistantContent.ToString(), ct);
        }
    }
}
