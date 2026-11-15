using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(Ct);
        body!.Keys.Should().BeEquivalentTo(["nextReportSeconds"], "the Satelles app reads exactly this field");
        body["nextReportSeconds"].GetInt32().Should().Be(30);

        var status = (await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!.Should().ContainSingle().Subject;
        status.IsOnline.Should().BeTrue();
        status.Report!.MachineName.Should().Be("DESKTOP-REPORT");
        status.Report.CpuPercent.Should().Be(12.5);
        status.Report.Disks.Should().ContainSingle().Which.Name.Should().Be("C:");
        status.LastSeenAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task PairedButSilentDevice_IsListedOffline_WithoutAReport()
    {
        var (owner, _) = await PairAsync("DESKTOP-QUIET");
        using var portal = factory.CreateClientFor(owner);

        var status = (await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!.Should().ContainSingle().Subject;

        status.IsOnline.Should().BeFalse();
        status.Report.Should().BeNull();
    }

    [Fact]
    public async Task InvalidReport_IsRejected()
    {
        var (_, device) = await PairAsync("DESKTOP-BAD");

        using var response = await PostReportAsync(device, SatellesReportJson.Replace("12.5", "250", StringComparison.Ordinal));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        bobSees.Should().ContainSingle().Which.Name.Should().Be("BOB-DESKTOP");

        using var alicePortal = factory.CreateClientFor(alice);
        var aliceDeviceId = (await alicePortal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!.Single().Id;
        (await bobPortal.GetAsync($"{Devices}/{aliceDeviceId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var sovereign = factory.CreateClient();
        var sovereignSees = (await sovereign.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!;
        sovereignSees.Select(d => d.Name).Should().Contain(["ALICE-DESKTOP", "BOB-DESKTOP"]);
        (await sovereign.GetAsync($"{Devices}/{aliceDeviceId}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemovingTheDevice_DropsItsState_AndItsNextReportGets401()
    {
        var (owner, device) = await PairAsync("DESKTOP-REMOVED");
        (await PostReportAsync(device, SatellesReportJson)).EnsureSuccessStatusCode();
        using var portal = factory.CreateClientFor(owner);
        var id = (await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct))!.Single().Id;

        (await portal.DeleteAsync($"/api/identity/devices/{id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await portal.GetFromJsonAsync<List<DeviceStatusDto>>(Devices, WebJson, Ct)).Should().BeEmpty();
        (await PostReportAsync(device, SatellesReportJson)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
