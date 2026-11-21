using System.Text.Json;
using System.Text.Json.Serialization;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Sentium.Infrastructure.Inference;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// <see cref="ILlamaMetricsClient"/> over <c>GET /models</c> and <c>GET /metrics</c>. The router lists each model
/// with a status and serves its metrics under <c>?model=</c>; the single-model embeddings server lists its model
/// without one.
/// </summary>
internal sealed class LlamaMetricsClient(IHttpClientFactory httpClientFactory, ILogger<LlamaMetricsClient> logger) : ILlamaMetricsClient
{
    public const string ChatClientName = "vigil-llm-metrics";
    public const string EmbeddingsClientName = "vigil-embeddings-metrics";

    public async Task<IReadOnlyList<ServedModel>?> ListModelsAsync(InferenceEndpoint server, CancellationToken ct)
    {
        try
        {
            using var response = await CreateClient(server).GetAsync(new Uri("models", UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("{Server} inference server returned {StatusCode} for the model list", server, (int)response.StatusCode);
                return null;
            }

            var list = await response.Content.ReadFromJsonAsync<ModelList>(ct);

            return (list?.Data ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .Select(ToServedModel)
                .ToList();
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogDebug(ex, "{Server} inference server unavailable while listing models", server);
            return null;
        }
    }

    public async Task<LlamaServerSnapshot?> GetSnapshotAsync(InferenceEndpoint server, ServedModel model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(model);

        var path = model.IsRouted ? $"metrics?model={Uri.EscapeDataString(model.Id)}" : "metrics";

        try
        {
            using var response = await CreateClient(server).GetAsync(new Uri(path, UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("{Server} inference server returned {StatusCode} for the metrics of {Model}", server, (int)response.StatusCode, model.Id);
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(ct);
            return LlamaServerSnapshot.FromSamples(PrometheusText.ParseUnlabelled(text));
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogDebug(ex, "{Server} inference server unavailable while reading the metrics of {Model}", server, model.Id);
            return null;
        }
    }

    private HttpClient CreateClient(InferenceEndpoint server)
    {
        return httpClientFactory.CreateClient(server == InferenceEndpoint.Chat ? ChatClientName : EmbeddingsClientName);
    }

    private static ServedModel ToServedModel(ListedModel model)
    {
        return model.Status is { } status
            ? new ServedModel(model.Id!, status.Value ?? "unknown", IsRouted: true, Failed: status.Failed == true)
            : new ServedModel(model.Id!, ServedModel.Loaded, IsRouted: false, Failed: false);
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

    private sealed record ModelList([property: JsonPropertyName("data")] List<ListedModel>? Data);

    private sealed record ListedModel(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] ListedModelStatus? Status);

    private sealed record ListedModelStatus(
        [property: JsonPropertyName("value")] string? Value,
        [property: JsonPropertyName("failed")] bool? Failed);
}
