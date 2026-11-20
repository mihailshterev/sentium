using System.Net;
using System.Net.Http.Json;
using Sentium.Praxis.Features.Skills;
using Sentium.Core.Results;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

public sealed class SkillsApiTests(MonolithWebApplicationFactory factory)
    : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetBuiltInSkills_Returns200_WithList()
    {
        var response = await _client.GetAsync("/api/praxis/skills/built-in", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var skills = await response.Content.ReadFromJsonAsync<List<BuiltInSkillInfo>>(Ct);
        Assert.NotNull(skills);
    }

    [Fact]
    public async Task CreateSkill_ReturnsCreated()
    {
        var request = new CreateAgentSkillRequest($"skill-{Guid.NewGuid()}", "A test skill", "Do something useful", AgentSkillType.Custom);

        var response = await _client.PostAsJsonAsync("/api/praxis/skills", request, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AgentSkillDto>(Ct);
        Assert.NotNull(dto);
        Assert.Equal(request.Name, dto!.Name);
    }

    [Fact]
    public async Task GetSkills_AfterCreation_IncludesNew()
    {
        var name = $"list-skill-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/praxis/skills", new CreateAgentSkillRequest(name, "desc", "instructions", AgentSkillType.Custom), Ct);

        var response = await _client.GetAsync("/api/praxis/skills", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<AgentSkillDto>>(Ct);
        Assert.Contains(paged!.Items, s => s.Name == name);
    }

    [Fact]
    public async Task CreateSkill_Returns409_OnDuplicateName()
    {
        var name = $"dup-skill-{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/praxis/skills", new CreateAgentSkillRequest(name, "desc", "inst", AgentSkillType.Custom), Ct);

        var response = await _client.PostAsJsonAsync("/api/praxis/skills", new CreateAgentSkillRequest(name, "desc2", "inst2", AgentSkillType.Custom), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSkill_ReturnsNoContent()
    {
        var createResp = await _client.PostAsJsonAsync("/api/praxis/skills", new CreateAgentSkillRequest($"update-me-{Guid.NewGuid()}", "desc", "inst", AgentSkillType.Custom), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<AgentSkillDto>(Ct);

        var response = await _client.PutAsJsonAsync($"/api/praxis/skills/{created!.Id}", new UpdateAgentSkillRequest("Updated Skill", "updated desc", "updated instructions"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_ReturnsNoContent_ThenGetReturns404()
    {
        var createResp = await _client.PostAsJsonAsync("/api/praxis/skills", new CreateAgentSkillRequest($"delete-me-{Guid.NewGuid()}", "desc", "inst", AgentSkillType.Custom), Ct);
        var created = await createResp.Content.ReadFromJsonAsync<AgentSkillDto>(Ct);

        var deleteResp = await _client.DeleteAsync($"/api/praxis/skills/{created!.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await _client.GetAsync($"/api/praxis/skills/{created.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }
}
