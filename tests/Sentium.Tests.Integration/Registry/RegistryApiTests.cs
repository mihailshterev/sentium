using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sentium.Registry.Core.Settings;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Registry;

public sealed class RegistryApiTests(RegistryTestFactory factory) : IClassFixture<RegistryTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Envelope(string Key, HarnessSettings Value, DateTimeOffset UpdatedAt, string? UpdatedBy);

    [Fact]
    public async Task GetHarness_ReturnsOk_OnFirstCall()
    {
        var response = await _client.GetAsync("/settings/harness", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHarness_ReturnsDefaults_WhenNoRowExists()
    {
        var response = await _client.GetAsync("/settings/harness", Ct);

        var env = await response.Content.ReadFromJsonAsync<Envelope>(Ct);
        env.Should().NotBeNull();
        env!.Key.Should().Be("harness");
        env.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateHarness_ReturnsOk_WithAppliedChanges()
    {
        var payload = new HarnessSettings { UserHarnessPrompt = "my custom prompt", IsBuiltInHarnessEnabled = true, IsPromptEnhancementEnabled = false };

        var response = await _client.PutAsJsonAsync("/settings/harness", payload, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<Envelope>(Ct);
        env!.Value.UserHarnessPrompt.Should().Be("my custom prompt");
    }

    [Fact]
    public async Task UpdateHarness_ThenGet_ReturnsPersisted()
    {
        var uniquePrompt = $"prompt-{Guid.NewGuid()}";
        var payload = new HarnessSettings { UserHarnessPrompt = uniquePrompt, IsBuiltInHarnessEnabled = true, IsPromptEnhancementEnabled = true };
        await _client.PutAsJsonAsync("/settings/harness", payload, Ct);

        var getResponse = await _client.GetAsync("/settings/harness", Ct);
        var env = await getResponse.Content.ReadFromJsonAsync<Envelope>(Ct);

        env!.Value.UserHarnessPrompt.Should().Be(uniquePrompt);
    }

    [Fact]
    public async Task UnknownKey_Returns404()
    {
        var response = await _client.GetAsync("/settings/does-not-exist", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
