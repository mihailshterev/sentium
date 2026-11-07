using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sentium.AppHost.Config;

/// <summary>
/// Readiness of the <c>llm</c> router: healthy once at least one chat model reports <c>loaded</c> on
/// <c>GET /models</c>. The router's own <c>/health</c> turns healthy before its models finish loading, so
/// <c>WaitFor(llm)</c> uses this check to start Sentium.Web only when the default model can answer
/// (the preset marks it <c>load-on-startup</c>).
/// </summary>
internal sealed class LlamaRouterReadyHealthCheck(EndpointReference endpoint, ParameterResource apiKey) : IHealthCheck
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!endpoint.IsAllocated)
        {
            return HealthCheckResult.Unhealthy("The llm endpoint is not allocated yet.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"{endpoint.Url}/models"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await apiKey.GetValueAsync(cancellationToken));

            using var response = await Http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy($"GET /models returned {(int)response.StatusCode}.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

            var loaded = json.RootElement.GetProperty("data").EnumerateArray()
                .Any(m => m.TryGetProperty("status", out var s) && s.TryGetProperty("value", out var v) && v.GetString() == "loaded");

            return loaded ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("No chat model is loaded yet.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("The llm router is not answering yet.", ex);
        }
    }
}
