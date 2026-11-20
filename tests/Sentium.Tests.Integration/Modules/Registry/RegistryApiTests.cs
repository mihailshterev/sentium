using System.Net;
using System.Net.Http.Json;
using Sentium.Registry.Contracts.Settings;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Registry;

public sealed class RegistryApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record SchemaSection(string Id, string Key);

    private sealed record Envelope(string Key, HarnessSettings Value, DateTimeOffset UpdatedAt, string? UpdatedBy);

    [Fact]
    public async Task GetHarness_ReturnsOk_OnFirstCall()
    {
        var response = await _client.GetAsync("/api/registry/settings/harness", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHarness_ReturnsDefaults_WhenNoRowExists()
    {
        var response = await _client.GetAsync("/api/registry/settings/harness", Ct);

        var env = await response.Content.ReadFromJsonAsync<Envelope>(Ct);
        Assert.NotNull(env);
        Assert.Equal("harness", env!.Key);
        Assert.NotNull(env.Value);
    }

    [Fact]
    public async Task UpdateHarness_ReturnsOk_WithAppliedChanges()
    {
        var payload = new HarnessSettings { UserHarnessPrompt = "my custom prompt", IsBuiltInHarnessEnabled = true, IsPromptEnhancementEnabled = false };

        var response = await _client.PutAsJsonAsync("/api/registry/settings/harness", payload, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var env = await response.Content.ReadFromJsonAsync<Envelope>(Ct);
        Assert.Equal("my custom prompt", env!.Value.UserHarnessPrompt);
    }

    [Fact]
    public async Task UpdateHarness_ThenGet_ReturnsPersisted()
    {
        var uniquePrompt = $"prompt-{Guid.NewGuid()}";
        var payload = new HarnessSettings { UserHarnessPrompt = uniquePrompt, IsBuiltInHarnessEnabled = true, IsPromptEnhancementEnabled = true };
        await _client.PutAsJsonAsync("/api/registry/settings/harness", payload, Ct);

        var getResponse = await _client.GetAsync("/api/registry/settings/harness", Ct);
        var env = await getResponse.Content.ReadFromJsonAsync<Envelope>(Ct);

        Assert.Equal(uniquePrompt, env!.Value.UserHarnessPrompt);
    }

    [Fact]
    public async Task GetSchema_ListsTheAgentsSection()
    {
        var response = await _client.GetAsync("/api/registry/settings/schema", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sections = await response.Content.ReadFromJsonAsync<List<SchemaSection>>(Ct);
        Assert.Contains(sections!, s => s.Id == "agents" && s.Key == "harness");
    }

    [Fact]
    public async Task UpdateHarness_OverTheLengthLimit_Returns400()
    {
        var payload = new HarnessSettings { UserHarnessPrompt = new string('x', HarnessSettings.MaxUserHarnessPromptLength + 1) };

        var response = await _client.PutAsJsonAsync("/api/registry/settings/harness", payload, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownKey_Returns404()
    {
        var response = await _client.GetAsync("/api/registry/settings/does-not-exist", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
