using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sentium.AgentRuntime.Core.Skills;
using Sentium.Shared.Results;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.AgentRuntime;

public sealed class SkillsApiTests(SentiumWebApplicationFactory<Sentium.AgentRuntime.Api.Program> factory)
    : IClassFixture<SentiumWebApplicationFactory<Sentium.AgentRuntime.Api.Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetBuiltInSkills_Returns200_WithList()
    {
        var response = await _client.GetAsync("/skills/built-in", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var skills = await response.Content.ReadFromJsonAsync<List<BuiltInSkillInfo>>(Ct);
        skills.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSkill_ReturnsCreated()
    {
        var request = new CreateAgentSkillRequest($"skill-{Guid.NewGuid()}", "A test skill", "Do something useful", AgentSkillType.Custom);

        var response = await _client.PostAsJsonAsync("/skills", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AgentSkillDto>(Ct);
        dto.Should().NotBeNull();
        dto!.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task GetSkills_AfterCreation_IncludesNew()
    {
        var name = $"list-skill-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/skills", new CreateAgentSkillRequest(name, "desc", "instructions", AgentSkillType.Custom), Ct);

        var response = await _client.GetAsync("/skills", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<AgentSkillDto>>(Ct);
        paged!.Items.Should().Contain(s => s.Name == name);
    }

    [Fact]
    public async Task CreateSkill_Returns409_OnDuplicateName()
    {
        var name = $"dup-skill-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/skills", new CreateAgentSkillRequest(name, "desc", "inst", AgentSkillType.Custom), Ct);

        var response = await _client.PostAsJsonAsync("/skills", new CreateAgentSkillRequest(name, "desc2", "inst2", AgentSkillType.Custom), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateSkill_ReturnsNoContent()
    {
        var createResp = await _client.PostAsJsonAsync("/skills", new CreateAgentSkillRequest($"update-me-{Guid.NewGuid()}", "desc", "inst", AgentSkillType.Custom), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<AgentSkillDto>(Ct);

        var response = await _client.PutAsJsonAsync($"/skills/{created!.Id}", new UpdateAgentSkillRequest("Updated Skill", "updated desc", "updated instructions"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteSkill_ReturnsNoContent_ThenGetReturns404()
    {
        var createResp = await _client.PostAsJsonAsync("/skills", new CreateAgentSkillRequest($"delete-me-{Guid.NewGuid()}", "desc", "inst", AgentSkillType.Custom), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<AgentSkillDto>(Ct);

        var deleteResp = await _client.DeleteAsync($"/skills/{created!.Id}", Ct);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await _client.GetAsync($"/skills/{created.Id}", Ct);
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
