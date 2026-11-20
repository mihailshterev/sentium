using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Identity.Features.Users;
using Sentium.Infrastructure.Security;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Identity;

/// <summary>
/// A Device token opens the Device endpoints and nothing else. This runs against the real authentication schemes
/// and walks every controller endpoint the host maps, so a new endpoint is covered the day it is added.
/// </summary>
public sealed partial class DeviceTokenScopeTests(RealAuthWebApplicationFactory factory) : IClassFixture<RealAuthWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DeviceToken_IsRejectedByEveryEndpointOutsideTheDeviceEndpoints()
    {
        var owner = await factory.CreateUserAsync();
        var device = await factory.PairDirectlyAsync(owner, "DESKTOP-SCOPE", Ct);
        using var client = factory.CreateDeviceClient(device.Token);

        var endpoints = ProtectedEndpoints(deviceEndpoints: false);
        Assert.True(endpoints.Count > 50, "the scan must find the module controllers");
        Assert.True(endpoints.Select(e => e.Module).Distinct().Count() >= 9, "every module has protected endpoints");

        List<string> accepted = [];
        foreach (var (method, path, _, contentType) in endpoints)
        {
            // An endpoint that only accepts some content type (multipart uploads) does not even match without it,
            // and the request would fall through to the API 404 instead of reaching authorization.
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (contentType is not null)
            {
                request.Content = new ByteArrayContent([]);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Ct);

            if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
            {
                accepted.Add($"{method} {path} -> {(int)response.StatusCode}");
            }
        }

        // a Device token must never be accepted outside the Device endpoints
        Assert.Empty(accepted);
    }

    [Fact]
    public async Task DeviceToken_OpensTheDeviceEndpoints()
    {
        var owner = await factory.CreateUserAsync();
        var device = await factory.PairDirectlyAsync(owner, "DESKTOP-OPEN", Ct);
        using var client = factory.CreateDeviceClient(device.Token);

        Assert.Equivalent(new string[] { "DELETE /api/identity/devices/current", "POST /api/vigil/devices/report", "GET /api/nuntius/devices/stream", "GET /api/nuntius/devices/unread-count", "GET /api/memoria/device/vault", "PUT /api/memoria/device/vault", "POST /api/memoria/device/vault/manifest", "PUT /api/memoria/device/vault/notes", "POST /api/memoria/device/vault/notes/remove", "POST /api/memoria/device/vault/notes/move" }, ProtectedEndpoints(deviceEndpoints: true).Select(e => $"{e.Method} {e.Path}"));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/identity/devices/current", Ct)).StatusCode);
    }

    [Fact]
    public async Task DeviceEndpoints_RejectAnUnknownToken_AndNoToken()
    {
        using var stranger = factory.CreateDeviceClient("not-a-device-token");
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);
    }

    [Fact]
    public async Task DeletingTheOwner_KillsTheDeviceToken()
    {
        var owner = await factory.CreateUserAsync();
        var device = await factory.PairDirectlyAsync(owner, "DESKTOP-ORPHAN", Ct);
        using var client = factory.CreateDeviceClient(device.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserService>();
            var (succeeded, errors) = await users.DeleteUserAsync(requesterId: Guid.NewGuid(), owner, Ct);
            Assert.True(succeeded, string.Join(", ", errors));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);
    }

    private List<(string Method, string Path, string Module, string? ContentType)> ProtectedEndpoints(bool deviceEndpoints)
    {
        return [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(e => NamesDeviceScheme(e) == deviceEndpoints)
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method => (
                Method: method,
                Path: "/" + RouteParameter().Replace(e.RoutePattern.RawText!, SampleValue),
                Module: e.Metadata.GetMetadata<ControllerActionDescriptor>()!.ControllerTypeInfo.Assembly.GetName().Name!,
                ContentType: e.Metadata.GetMetadata<IAcceptsMetadata>()?.ContentTypes.FirstOrDefault())))];
    }

    private static bool NamesDeviceScheme(RouteEndpoint endpoint)
    {
        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(a => a.AuthenticationSchemes == DeviceAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// A value that satisfies the parameter's route constraints, so the request matches the endpoint.
    /// </summary>
    private static string SampleValue(Match parameter)
    {
        if (parameter.Value.Contains(":guid", StringComparison.Ordinal))
        {
            return Guid.Empty.ToString();
        }

        var range = RangeConstraint().Match(parameter.Value);
        return range.Success ? range.Groups["min"].Value : "1";
    }

    [GeneratedRegex(@"range\((?<min>\d+),")]
    private static partial Regex RangeConstraint();

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex RouteParameter();
}
