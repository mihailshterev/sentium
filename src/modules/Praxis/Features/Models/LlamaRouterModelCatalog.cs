using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Sentium.Praxis.Features.Models;

/// <summary>
/// <see cref="IModelCatalog"/> over llama-server's router API (<c>GET /models</c>,
/// <c>POST /models/load</c>, <c>POST /models/unload</c>). Uses Praxis' chat-model HttpClient, so the base
/// address and API key are the ones inference uses.
/// </summary>
public sealed class LlamaRouterModelCatalog(HttpClient httpClient, ILogger<LlamaRouterModelCatalog> logger) : IModelCatalog
{
    public async Task<IReadOnlyList<CatalogModel>?> ListAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(new Uri("models", UriKind.Relative), ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Inference server returned {StatusCode} for the model list", (int)response.StatusCode);
                return null;
            }

            var list = await response.Content.ReadFromJsonAsync<RouterModelList>(ct);

            return (list?.Data ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .Select(m => new CatalogModel(m.Id!, m.Status?.Value ?? "unknown", m.Meta?.ContextLength))
                .OrderBy(m => m.Id, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogWarning(ex, "Inference server unavailable while listing models");
            return null;
        }
    }

    public Task<ModelActionResult> LoadAsync(string modelId, CancellationToken ct = default) => PostAsync("models/load", modelId, ct);

    public Task<ModelActionResult> UnloadAsync(string modelId, CancellationToken ct = default) => PostAsync("models/unload", modelId, ct);

    private async Task<ModelActionResult> PostAsync(string path, string modelId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(new Uri(path, UriKind.Relative), new RouterModelRequest(modelId), ct);

            if (response.IsSuccessStatusCode)
            {
                return ModelActionResult.Accepted;
            }

            // The router answers 404 for an undeclared model and 400 for a state conflict
            // ("model is already running" while a load or unload is still in progress).
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                logger.LogInformation("Inference server rejected {Path} for model {Model}: {StatusCode}", path, modelId, (int)response.StatusCode);
                return response.StatusCode == HttpStatusCode.NotFound ? ModelActionResult.NotFound : ModelActionResult.Conflict;
            }

            logger.LogWarning("Inference server returned {StatusCode} for {Path} of model {Model}", (int)response.StatusCode, path, modelId);
            return ModelActionResult.Failed;
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogWarning(ex, "Inference server unavailable for {Path} of model {Model}", path, modelId);
            return ModelActionResult.Failed;
        }
    }

    private static bool IsUnavailable(Exception ex, CancellationToken ct)
    {
        return ex switch
        {
            HttpRequestException or TimeoutRejectedException or BrokenCircuitException or JsonException => true,
            TaskCanceledException => !ct.IsCancellationRequested,
            _ => false
        };
    }

    private sealed record RouterModelList([property: JsonPropertyName("data")] List<RouterModel>? Data);

    private sealed record RouterModel(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] RouterModelStatus? Status,
        [property: JsonPropertyName("meta")] RouterModelMeta? Meta);

    // n_ctx is the per-slot context (preset ctx-size divided by parallel slots).
    private sealed record RouterModelMeta([property: JsonPropertyName("n_ctx")] int? ContextLength);

    private sealed record RouterModelStatus([property: JsonPropertyName("value")] string? Value);

    private sealed record RouterModelRequest([property: JsonPropertyName("model")] string Model);
}
