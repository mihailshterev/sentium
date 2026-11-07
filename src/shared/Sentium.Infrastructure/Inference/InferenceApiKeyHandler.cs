using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Sentium.Infrastructure.Inference;

/// <summary>
/// Adds <see cref="InferenceOptions.ApiKey"/> as a bearer token to requests on an inference HttpClient
/// that do not carry one, i.e. the raw management calls (the router's <c>/models</c>). Requests made
/// through the OpenAI SDK already carry the same key and are left untouched.
/// </summary>
internal sealed class InferenceApiKeyHandler(IOptions<InferenceOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = options.Value.ApiKey;

        if (request.Headers.Authorization is null && !string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
