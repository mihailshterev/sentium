using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sentium.Shared.Constants;

namespace Sentium.Infrastructure.Security;

/// <summary>
/// ASP.NET Core authentication handler for the <c>InternalApiKey</c> scheme.
/// Authenticates a request when the <c>X-Internal-Token</c> header matches the configured key.
/// </summary>
public sealed class InternalApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<InternalApiOptions> apiOptions,
    IHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = apiOptions.CurrentValue.ApiKey;

        if (string.IsNullOrEmpty(expected))
        {
            if (environment.IsDevelopment())
            {
                return Task.FromResult(AuthenticateResult.Success(CreateTicket()));
            }

            Logger.LogCritical("InternalApi:ApiKey is not configured - all internal service calls will be rejected.");

            return Task.FromResult(AuthenticateResult.Fail("Internal API key is not configured."));
        }

        var header = Request.Headers[CommonHeaderNames.InternalToken].ToString();

        if (header == expected)
        {
            return Task.FromResult(AuthenticateResult.Success(CreateTicket()));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }

    private AuthenticationTicket CreateTicket()
    {
        var identity = new ClaimsIdentity([new Claim("caller-type", "internal-system")], Scheme.Name);

        return new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
    }
}
