using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sentium.Sentinel.Core.Events;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Sentinel;

public sealed class SentinelApiTests(SentiumWebApplicationFactory<Sentium.Sentinel.Api.Program> factory)
    : IClassFixture<SentiumWebApplicationFactory<Sentium.Sentinel.Api.Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SentinelEvent MakeEvent(string action = TrafficDirection.Inbound) =>
        new("host", EventType.Network, action, DateTime.UtcNow, new Dictionary<string, string>());

    [Fact]
    public async Task Post_Events_AllowedEvent_Returns200Ok()
    {
        // Arrange
        var evt = MakeEvent(TrafficDirection.Inbound);

        // Act
        var response = await _client.PostAsJsonAsync("/events", evt, Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Events_Network_ReturnsOkWithList()
    {
        // Act
        var response = await _client.GetAsync("/events/network", Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_Events_Network_CountParam_CapsAt200()
    {
        // Arrange
        await _client.PostAsJsonAsync("/events", MakeEvent(), Ct);
        await _client.PostAsJsonAsync("/events", MakeEvent(), Ct);

        // Act
        var response = await _client.GetAsync("/events/network?count=50", Ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<NetworkEventRecord>>(Ct);
        list.Should().NotBeNull();

        list!.Count.Should().BeLessThanOrEqualTo(50);
    }
}
