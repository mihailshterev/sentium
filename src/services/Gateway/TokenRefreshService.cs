using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Sentium.ApiGateway;

public sealed class TokenRefreshService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    ILogger<TokenRefreshService> logger)
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshedTokens?>>> InflightRefreshes = new(StringComparer.Ordinal);

    public async Task<bool> TryRefreshAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var refreshToken = await context.GetTokenAsync("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        var inflight = InflightRefreshes.GetOrAdd(refreshToken, key => new Lazy<Task<RefreshedTokens?>>(() => RequestNewTokensAsync(context, key)));

        RefreshedTokens? tokens;
        try
        {
            tokens = await inflight.Value;
        }
        finally
        {
            InflightRefreshes.TryRemove(refreshToken, out _);
        }

        if (tokens is null)
        {
            return false;
        }

        return await PersistTokensAsync(context, tokens);
    }

    private async Task<RefreshedTokens?> RequestNewTokensAsync(HttpContext context, string refreshToken)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);

        if (options.ConfigurationManager is not Microsoft.IdentityModel.Protocols.IConfigurationManager<OpenIdConnectConfiguration> configManager)
        {
            return null;
        }

        OpenIdConnectConfiguration oidcConfig;
        try
        {
            oidcConfig = await configManager.GetConfigurationAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token refresh failed: could not retrieve OpenID Connect configuration.");
            return null;
        }

        if (string.IsNullOrEmpty(oidcConfig.TokenEndpoint))
        {
            logger.LogWarning("Token refresh failed: OpenID Connect configuration has no token endpoint.");
            return null;
        }

        var client = httpClientFactory.CreateClient("IdpClient");
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("client_id", options.ClientId!),
            new KeyValuePair<string, string>("client_secret", options.ClientSecret!),
        ]);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(oidcConfig.TokenEndpoint, content, context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token refresh failed: request to the token endpoint threw.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Token refresh failed: token endpoint returned {StatusCode}.", response.StatusCode);
            return null;
        }

        JsonElement tokenData;
        try
        {
            tokenData = await response.Content.ReadFromJsonAsync<JsonElement>(context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token refresh failed: could not parse the token endpoint response.");
            return null;
        }

        if (!tokenData.TryGetProperty("access_token", out var accessTokenProp))
        {
            logger.LogWarning("Token refresh failed: token endpoint response contained no access_token.");
            return null;
        }
        var newAccessToken = accessTokenProp.GetString();
        if (string.IsNullOrEmpty(newAccessToken))
        {
            logger.LogWarning("Token refresh failed: token endpoint returned an empty access_token.");
            return null;
        }

        var newRefreshToken = tokenData.TryGetProperty("refresh_token", out var rtProp)
            ? rtProp.GetString() ?? refreshToken
            : refreshToken;

        var expiresIn = tokenData.TryGetProperty("expires_in", out var eiProp) ? eiProp.GetInt32() : 3600;
        var newExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString("r", CultureInfo.InvariantCulture);

        return new RefreshedTokens(newAccessToken, newRefreshToken, newExpiresAt);
    }

    private async Task<bool> PersistTokensAsync(HttpContext context, RefreshedTokens tokens)
    {
        var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (authResult.Principal is null)
        {
            logger.LogWarning("Token refresh failed: could not re-authenticate the current principal to persist new tokens.");
            return false;
        }

        authResult.Properties!.UpdateTokenValue("access_token", tokens.AccessToken);
        authResult.Properties!.UpdateTokenValue("refresh_token", tokens.RefreshToken);
        authResult.Properties!.UpdateTokenValue("expires_at", tokens.ExpiresAt);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authResult.Principal,
            authResult.Properties);

        return true;
    }

    private sealed record RefreshedTokens(string AccessToken, string RefreshToken, string ExpiresAt);
}
