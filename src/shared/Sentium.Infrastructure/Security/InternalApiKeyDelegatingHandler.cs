using Microsoft.Extensions.Options;
using Sentium.Shared.Constants;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// Outgoing HTTP handler that attaches the <c>X-Internal-Token</c> header to every request so
/// service-to-service callers satisfy the <see cref="Policies.SystemCaller"/> policy on the
/// receiving end.
/// </summary>
public sealed class InternalApiKeyDelegatingHandler(IOptionsMonitor<InternalApiOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = options.CurrentValue.ApiKey;
        if (!string.IsNullOrEmpty(key))
        {
            request.Headers.TryAddWithoutValidation(CommonHeaderNames.InternalToken, key);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
