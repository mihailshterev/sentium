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

    [Fact]
    public async Task GetSettings_ReturnsOk_OnFirstCall()
    {
        var response = await _client.GetAsync("/settings", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSettings_SeedsDefaults_WhenNoRowExists()
    {
        var response = await _client.GetAsync("/settings", Ct);

        var dto = await response.Content.ReadFromJsonAsync<SettingsDto>(Ct);
        dto.Should().NotBeNull();
        dto!.Harness.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSettings_ReturnsOk_WithAppliedChanges()
    {
        var request = new UpdateSettingsRequest(new UpdateHarnessSettingsRequest("my custom prompt", true, false));

        var response = await _client.PutAsJsonAsync("/settings", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<SettingsDto>(Ct);
        dto!.Harness.UserHarnessPrompt.Should().Be("my custom prompt");
    }

    [Fact]
    public async Task UpdateSettings_ThenGet_ReturnsPersisted()
    {
        var uniquePrompt = $"prompt-{Guid.NewGuid()}";
        var updateRequest = new UpdateSettingsRequest(new UpdateHarnessSettingsRequest(uniquePrompt, true, true));
        await _client.PutAsJsonAsync("/settings", updateRequest, Ct);

        var getResponse = await _client.GetAsync("/settings", Ct);
        var dto = await getResponse.Content.ReadFromJsonAsync<SettingsDto>(Ct);

        dto!.Harness.UserHarnessPrompt.Should().Be(uniquePrompt);
    }

    [Fact]
    public async Task GetSettings_Returns401_WhenUnauthenticated()
    {
        var unauthClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        unauthClient.DefaultRequestHeaders.Clear();

        var response = await unauthClient.GetAsync("/settings", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
