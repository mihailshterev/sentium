using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Sentium.Tests.Integration.Common;
using Sentium.Tests.Integration.Modules.Identity;
using Sentium.Vigil.Features.Devices;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Vigil;

/// <summary>
/// Device Reports end to end. Reports are posted as raw JSON in exactly the shape the Satelles app sends, so a rename on
/// the server side fails here instead of silently breaking paired PCs.
/// </summary>
public sealed class DeviceReportsApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Devices = "/api/vigil/devices";

    private const string SatellesReportJson = """
        {
          "machineName": "DESKTOP-REPORT",
          "osDescription": "Windows 11 Pro",
          "uptimeSeconds": 3600,
          "appVersion": "1.2.3",
          "cpuPercent": 12.5,
          "memoryTotalBytes": 17179869184,
          "memoryAvailableBytes": 8589934592,
          "disks": [ { "name": "C:", "totalBytes": 512000000000, "availableBytes": 256000000000 } ]
        }
        """;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Report_IsAccepted_AndTheDeviceShowsOnlineWithItsFigures()
    {
        var (owner, device) = await PairAsync("DESKTOP-REPORT");
        using var portal = factory.CreateClientFor(owner);

        using var response = await PostReportAsync(device, SatellesReportJson);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(Ct);
        // the Satelles app reads exactly this field
        Assert.Equivalent(new[] { "nextReportSeconds" }, body!.Keys);
        Assert.Equal(30, body["nextReportSeconds"].GetInt32());

        var status = Assert.Single((await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!);
        Assert.True(status.IsOnline);
        Assert.Equal("DESKTOP-REPORT", status.Report!.MachineName);
        Assert.Equal(12.5, status.Report.CpuPercent);
        Assert.Equal("C:", Assert.Single(status.Report.Disks).Name);
        Assert.Equal(DateTimeOffset.UtcNow, Assert.NotNull(status.LastSeenAt), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task PairedButSilentDevice_IsListedOffline_WithoutAReport()
    {
        var (owner, _) = await PairAsync("DESKTOP-QUIET");
        using var portal = factory.CreateClientFor(owner);

        var status = Assert.Single((await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!);

        Assert.False(status.IsOnline);
        Assert.Null(status.Report);
    }

    [Fact]
    public async Task InvalidReport_IsRejected()
    {
        var (_, device) = await PairAsync("DESKTOP-BAD");

        using var response = await PostReportAsync(device, SatellesReportJson.Replace("12.5", "250", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Members_SeeOnlyTheirOwnDevices_SovereignsAll()
    {
        var (alice, aliceDevice) = await PairAsync("ALICE-DESKTOP");
        var (bob, bobDevice) = await PairAsync("BOB-DESKTOP");
        (await PostReportAsync(aliceDevice, SatellesReportJson)).EnsureSuccessStatusCode();
        (await PostReportAsync(bobDevice, SatellesReportJson)).EnsureSuccessStatusCode();

        using var bobPortal = factory.CreateClientFor(bob);
        var bobSees = (await bobPortal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!;
        Assert.Equal("BOB-DESKTOP", Assert.Single(bobSees).Name);

        using var alicePortal = factory.CreateClientFor(alice);
        var aliceDeviceId = (await alicePortal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!.Single().Id;
        Assert.Equal(HttpStatusCode.NotFound, (await bobPortal.GetAsync($"{Devices}/{aliceDeviceId}", Ct)).StatusCode);

        using var sovereign = factory.CreateClient();
        var sovereignSees = (await sovereign.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!;
        Assert.All(new[] { "ALICE-DESKTOP", "BOB-DESKTOP" }, expected => Assert.Contains(expected, sovereignSees.Select(d => d.Name)));
        Assert.Equal(HttpStatusCode.OK, (await sovereign.GetAsync($"{Devices}/{aliceDeviceId}", Ct)).StatusCode);
    }

    [Fact]
    public async Task RemovingTheDevice_DropsItsState_AndItsNextReportGets401()
    {
        var (owner, device) = await PairAsync("DESKTOP-REMOVED");
        (await PostReportAsync(device, SatellesReportJson)).EnsureSuccessStatusCode();
        using var portal = factory.CreateClientFor(owner);
        var id = (await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!.Single().Id;

        Assert.Equal(HttpStatusCode.NoContent, (await portal.DeleteAsync($"/api/identity/devices/{id}", Ct)).StatusCode);

        Assert.Empty((await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostReportAsync(device, SatellesReportJson)).StatusCode);
    }

    private async Task<(Guid Owner, HttpClient Device)> PairAsync(string name)
    {
        var owner = await factory.CreateUserAsync();
        using var portal = factory.CreateClientFor(owner);
        var paired = await factory.PairAsync(portal, name, Ct);
        return (owner, factory.CreateDeviceClient(paired.Token));
    }

    private static async Task<HttpResponseMessage> PostReportAsync(HttpClient device, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await device.PostAsync($"{Devices}/report", content, Ct);
    }
}
