using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Devices;
using Sentium.Tests.Integration.Common;

namespace Sentium.Tests.Integration.Modules.Identity;

/// <summary>
/// Devices belong to real user rows (the foreign key is what removes them with the account), so Device tests
/// create their users instead of inventing ids like the other suites do.
/// </summary>
internal static class DeviceTestSupport
{
    public const string SatellesUserAgent = "SentiumSatelles/1.2.3";

    public static async Task<Guid> CreateUserAsync(this MonolithWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Device", LastName = "Owner" };
        user.UserName = $"owner-{user.Id:N}";

        var result = await users.CreateAsync(user);
        result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors.Select(e => e.Description)));

        return user.Id;
    }

    /// <summary>
    /// Pairs a Device through the service, for suites where no request can sign in as a portal user.
    /// </summary>
    public static async Task<PairDeviceResponse> PairDirectlyAsync(this MonolithWebApplicationFactory factory, Guid userId, string name, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        var code = await devices.CreatePairingCodeAsync(userId, ct);
        code.IsSuccess.Should().BeTrue(code.Error);

        var paired = await devices.PairAsync(code.Value!.Code, name, appVersion: null, ct);
        paired.IsSuccess.Should().BeTrue(paired.Error);

        return paired.Value!;
    }

    public static HttpClient CreateDeviceClient(this MonolithWebApplicationFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(SatellesUserAgent);
        return client;
    }

    public static HttpClient CreateClientFor(this MonolithWebApplicationFactory factory, Guid memberId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, memberId.ToString());
        return client;
    }

    public static async Task<PairDeviceResponse> PairAsync(this MonolithWebApplicationFactory factory, HttpClient owner, string name, CancellationToken ct)
    {
        var codeResponse = await owner.PostAsync("/api/identity/devices/pairing-codes", null, ct);
        codeResponse.EnsureSuccessStatusCode();
        var code = (await codeResponse.Content.ReadFromJsonAsync<PairingCodeResponse>(ct))!;

        using var app = factory.CreateClient();
        app.DefaultRequestHeaders.UserAgent.ParseAdd(SatellesUserAgent);

        var pairResponse = await app.PostAsJsonAsync("/api/identity/devices/pair", new PairDeviceRequest(code.Code, name), ct);
        pairResponse.EnsureSuccessStatusCode();

        return (await pairResponse.Content.ReadFromJsonAsync<PairDeviceResponse>(ct))!;
    }
}
