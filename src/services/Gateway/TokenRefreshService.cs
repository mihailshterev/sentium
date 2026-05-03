using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Globalization;
using System.Text.Json;

namespace Sentium.ApiGateway;

public sealed class TokenRefreshService(IHttpClientFactory httpClientFactory, IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
{
    public async Task<bool> TryRefreshAsync(HttpContext context)
    {
        var refreshToken = await context.GetTokenAsync("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);

        if (options.ConfigurationManager is not Microsoft.IdentityModel.Protocols.IConfigurationManager<OpenIdConnectConfiguration> configManager)
        {
            return false;
        }

        OpenIdConnectConfiguration oidcConfig;
        try
        {
            oidcConfig = await configManager.GetConfigurationAsync(CancellationToken.None);
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrEmpty(oidcConfig.TokenEndpoint))
        {
            return false;
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
            response = await client.PostAsync(oidcConfig.TokenEndpoint, content);
        }
        catch
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        JsonElement tokenData;
        try
        {
            tokenData = await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch
        {
            return false;
        }

        if (!tokenData.TryGetProperty("access_token", out var accessTokenProp))
        {
            return false;
        }
        var newAccessToken = accessTokenProp.GetString();
        if (string.IsNullOrEmpty(newAccessToken))
        {
            return false;
        }

        var newRefreshToken = tokenData.TryGetProperty("refresh_token", out var rtProp)
            ? rtProp.GetString() ?? refreshToken
            : refreshToken;

        var expiresIn = tokenData.TryGetProperty("expires_in", out var eiProp) ? eiProp.GetInt32() : 3600;
        var newExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToString("r", CultureInfo.InvariantCulture);

        var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (authResult.Principal is null)
        {
            return false;
        }

        authResult.Properties!.UpdateTokenValue("access_token", newAccessToken);
        authResult.Properties!.UpdateTokenValue("refresh_token", newRefreshToken);
        authResult.Properties!.UpdateTokenValue("expires_at", newExpiresAt);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authResult.Principal,
            authResult.Properties);

        return true;
    }
}
