using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Identity.Contracts;
using Sentium.Identity.Data;
using Sentium.Identity.Features.Devices;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Identity;

/// <summary>
/// Pairing and managing Devices through <c>/api/identity/devices</c>. Portal calls act as fresh Members (see
/// <see cref="TestAuthHandler.UserHeader"/>) or the default Sovereign; Device calls send their own Bearer token,
/// which the Device endpoints authenticate with the real Device scheme.
/// </summary>
public sealed class DevicesApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Devices = "/api/identity/devices";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pair_WithAValidCode_ReturnsAWorkingToken_AndRecordsTheDevice()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);

        var paired = await factory.PairAsync(portal, "DESKTOP-PAIR", Ct);

        Assert.False(string.IsNullOrWhiteSpace(paired.Token));
        using var device = factory.CreateDeviceClient(paired.Token);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);

        var listed = Assert.Single((await portal.GetFromJsonAsync<List<DeviceInfo>>(Devices, Ct))!);
        Assert.Equal(paired.DeviceId, listed.Id);
        Assert.Equal("DESKTOP-PAIR", listed.Name);
        Assert.Equal(owner, listed.OwnerUserId);
        // the Satelles app's User-Agent carries its version
        Assert.Equal("1.2.3", listed.AppVersion);
    }

    [Fact]
    public async Task PairingCode_WorksOnce()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var code = await CreateCodeAsync(portal);

        using var app = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsJsonAsync($"{Devices}/pair", new PairDeviceRequest(code.Code, "First"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsJsonAsync($"{Devices}/pair", new PairDeviceRequest(code.Code, "Second"), Ct)).StatusCode);
    }

    [Fact]
    public async Task PairingCode_IsRejectedOnceExpired()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var code = await CreateCodeAsync(portal);
        Assert.Equal(DateTimeOffset.UtcNow.AddMinutes(10), code.ExpiresAt, TimeSpan.FromMinutes(1));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.DevicePairingCodes
                .Where(c => c.UserId == owner)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)), Ct);
        }

        using var app = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsJsonAsync($"{Devices}/pair", new PairDeviceRequest(code.Code, "Late"), Ct)).StatusCode);
    }

    [Fact]
    public async Task PairingCode_IgnoresCaseAndSeparators_AndANewCodeReplacesTheOld()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var first = await CreateCodeAsync(portal);
        var second = await CreateCodeAsync(portal);

        using var app = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await app.PostAsJsonAsync($"{Devices}/pair", new PairDeviceRequest(first.Code, "Old code"), Ct)).StatusCode);

        var typed = $"{second.Code[..4].ToLowerInvariant()}-{second.Code[4..].ToLowerInvariant()}";
        Assert.Equal(HttpStatusCode.OK, (await app.PostAsJsonAsync($"{Devices}/pair", new PairDeviceRequest(typed, "Typed"), Ct)).StatusCode);
    }

    [Fact]
    public async Task Pairing_StopsAtTenDevices()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);

        for (var i = 0; i < 10; i++)
        {
            await factory.PairAsync(portal, $"PC-{i}", Ct);
        }

        Assert.Equal(HttpStatusCode.Conflict, (await portal.PostAsync($"{Devices}/pairing-codes", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task RemovingADevice_KillsItsToken()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var paired = await factory.PairAsync(portal, "DESKTOP-GONE", Ct);
        using var device = factory.CreateDeviceClient(paired.Token);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"{Devices}/{paired.DeviceId}", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/nuntius/devices/unread-count", Ct)).StatusCode);
        Assert.Empty((await portal.GetFromJsonAsync<List<DeviceInfo>>(Devices, Ct))!);
    }

    [Fact]
    public async Task TheAppUnpairsItself()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var paired = await factory.PairAsync(portal, "DESKTOP-SELF", Ct);
        using var device = factory.CreateDeviceClient(paired.Token);

        Assert.Equal(HttpStatusCode.NoContent, (await device.DeleteAsync($"{Devices}/current", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await device.DeleteAsync($"{Devices}/current", Ct)).StatusCode);
        Assert.Empty((await portal.GetFromJsonAsync<List<DeviceInfo>>(Devices, Ct))!);
    }

    [Fact]
    public async Task Members_SeeAndRemoveOnlyTheirOwnDevices_SovereignsAll()
    {
        var alice = await factory.CreateUserAsync();
        var bob = await factory.CreateUserAsync();
        using var alicePortal = factory.CreateClientFor(alice);
        using var bobPortal = factory.CreateClientFor(bob);
        using var sovereign = factory.CreateClient();
        var alicePc = await factory.PairAsync(alicePortal, "ALICE-PC", Ct);
        var bobPc = await factory.PairAsync(bobPortal, "BOB-PC", Ct);

        Assert.Equal(new Guid[] { bobPc.DeviceId }, (await bobPortal.GetFromJsonAsync<List<DeviceInfo>>(Devices, Ct))!.Select(d => d.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await bobPortal.GetAsync($"{Devices}?all=true", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bobPortal.DeleteAsync($"{Devices}/{alicePc.DeviceId}", Ct)).StatusCode);

        var all = (await sovereign.GetFromJsonAsync<List<DeviceInfo>>($"{Devices}?all=true", Ct))!;
        Assert.All(new[] { alicePc.DeviceId, bobPc.DeviceId }, expected => Assert.Contains(expected, all.Select(d => d.Id)));
        Assert.StartsWith("owner-", all.Single(d => d.Id == alicePc.DeviceId).OwnerUserName, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NoContent, (await sovereign.DeleteAsync($"{Devices}/{alicePc.DeviceId}", Ct)).StatusCode);
        Assert.Empty((await alicePortal.GetFromJsonAsync<List<DeviceInfo>>(Devices, Ct))!);
    }

    [Fact]
    public async Task PairResponse_HasTheShapeTheSatellesAppReads()
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var code = await CreateCodeAsync(portal);

        using var app = factory.CreateClient();
        using var response = await app.PostAsJsonAsync($"{Devices}/pair", new { code = code.Code, name = "SHAPE" }, Ct);

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Ct);
        Assert.Equivalent(new string[] { "deviceId", "token" }, json!.Keys);
    }

    private async Task<PairingCodeResponse> CreateCodeAsync(HttpClient portal)
    {
        var response = await portal.PostAsync($"{Devices}/pairing-codes", null, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PairingCodeResponse>(Ct))!;
    }
}
