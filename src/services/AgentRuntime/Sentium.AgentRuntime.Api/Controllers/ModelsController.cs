using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sentium.AgentRuntime.Core.Agents;
using Sentium.AgentRuntime.Core.Rag;
using Sentium.AgentRuntime.Infrastructure;
using Sentium.Infrastructure.Security;
using Sentium.Shared.Constants;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentium.AgentRuntime.Api.Controllers;

/// <summary>
/// Provides endpoints for managing local Ollama models, including listing, pulling, and deleting.
/// </summary>
[ApiController]
[Authorize]
[Route("models")]
public sealed class ModelsController(
    IHttpClientFactory httpClientFactory,
    IAgentRepository agentRepository,
    OllamaOptions ollamaOptions,
    IOptions<RagOptions> ragOptions) : ControllerBase
{
    private Uri OllamaBase => ollamaOptions.BaseUrl;
    private string EmbeddingModel => ragOptions.Value.EmbeddingModelName;
    private bool IsSovereign => RoleClaims.IsInRole(User, SecurityRoles.Sovereign);

    /// <summary>
    /// Retrieves a list of all models currently installed on the local Ollama instance.
    /// </summary>
    /// <param name="ct">Cancellation token to abort the request.</param>
    /// <returns>A JSON array of installed models.</returns>
    /// <response code="200">Returns the list of models.</response>
    /// <response code="500">If the Ollama service is unreachable or returns an error.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetModels(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(ResourceNames.Ollama);
        using var response = await client.GetAsync(new Uri(OllamaBase, "/api/tags"), ct);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var tagsResponse = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(ct);
        if (tagsResponse is null)
        {
            return Ok(Array.Empty<OllamaModelInfo>());
        }

        var filtered = tagsResponse.Models
            .Where(m => !m.Name.StartsWith(EmbeddingModel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Ok(filtered);
    }

    /// <summary>
    /// Pulls (downloads) a model from the Ollama library.
    /// </summary>
    /// <remarks>
    /// This endpoint streams progress updates using the <c>application/x-ndjson</c> format.
    /// Each line in the response is a JSON object containing download status and progress.
    /// </remarks>
    /// <param name="request">The request containing the name of the model to pull.</param>
    /// <param name="ct">Cancellation token to abort the download.</param>
    /// <response code="200">The download has started and is being streamed.</response>
    /// <response code="400">If the request body is invalid or the model name is missing.</response>
    [HttpPost("pull")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task PullModel([FromBody] PullModelRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsSovereign)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var ollamaClient = httpClientFactory.CreateClient(ResourceNames.Ollama);

        Response.ContentType = "application/x-ndjson";
        Response.Headers.CacheControl = "no-cache";

        var payload = JsonSerializer.Serialize(new { name = request.Name, stream = true });

        using var pullContent = new StringContent(payload, Encoding.UTF8, "application/json");

        using var pullRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(OllamaBase, "/api/pull"))
        {
            Content = pullContent
        };

        using var ollamaResponse = await ollamaClient.SendAsync(pullRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!ollamaResponse.IsSuccessStatusCode)
        {
            Response.StatusCode = (int)ollamaResponse.StatusCode;
            return;
        }

        await using var stream = await ollamaResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await Response.WriteAsync(line + "\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    /// <summary>
    /// Deletes a model from the local Ollama instance and resets any agents using it to a default model.
    /// </summary>
    /// <param name="name">The name of the model to delete (e.g., "llama3").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result object containing deletion details and the number of agents affected.</returns>
    /// <response code="200">Model deleted successfully and agents reset.</response>
    /// <response code="400">If the model name is missing.</response>
    [HttpDelete]
    [ProducesResponseType(typeof(DeleteModelResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteModel([FromQuery] string name, CancellationToken ct)
    {
        if (!IsSovereign)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Model name is required.");
        }

        var ollamaClient = httpClientFactory.CreateClient(ResourceNames.Ollama);

        var payload = JsonSerializer.Serialize(new { name });
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(OllamaBase, "/api/delete"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await ollamaClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var resetCount = await agentRepository.ResetAgentsModelAsync(name, ollamaOptions.DefaultModel, ct);

        return Ok(new DeleteModelResult(name, ollamaOptions.DefaultModel, resetCount));
    }
}

public sealed record PullModelRequest(string Name);

public sealed record DeleteModelResult(string DeletedModel, string DefaultModel, int AgentsReset);

public sealed record OllamaTagsResponse(
    [property: JsonPropertyName("models")] List<OllamaModelInfo> Models);

public sealed record OllamaModelInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("modified_at")] string ModifiedAt,
    [property: JsonPropertyName("details")] OllamaModelDetails? Details);

public sealed record OllamaModelDetails(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("parameter_size")] string ParameterSize,
    [property: JsonPropertyName("quantization_level")] string QuantizationLevel);
