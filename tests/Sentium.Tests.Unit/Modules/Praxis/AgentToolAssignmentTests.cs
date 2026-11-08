using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Tools;
using Sentium.Tests.Unit.Modules.Praxis.Support;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

/// <summary>
/// Tools assigned in the portal decide what the model is offered: a custom agent's own list, a user's override of a
/// built-in's code-defined list, or (no override) the code default.
/// </summary>
public sealed class AgentToolAssignmentTests
{
    private readonly AgentTestHost _host = new();
    private readonly Guid _userId = Guid.NewGuid();

    public AgentToolAssignmentTests()
    {
        _host.RunContext.UserId = _userId;
        foreach (var name in new[] { ToolNames.KnowledgeBaseSearch, ToolNames.RecallLearnings, ToolNames.FindAssets, ToolNames.GetTimelineOverview })
        {
            _host.Tools.Add(AIFunctionFactory.Create(() => "ok", name, $"{name} description"));
        }
    }

    [Fact]
    public async Task CustomAgent_GetsOnlyItsAssignedTools()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.Agents.GetAgentByNameForUserAsync("Scout", _userId, Arg.Any<CancellationToken>())
            .Returns(new AgentResponse(Guid.NewGuid(), "Scout", "Finds things.", "", DateTime.UtcNow, DateTime.UtcNow, [ToolNames.FindAssets]));

        var offered = await OfferedToolsAsync("Scout", ct);

        offered.Should().Equal(ToolNames.FindAssets);
    }

    [Fact]
    public async Task CustomAgent_WithoutAList_GetsEveryTool()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.Agents.GetAgentByNameForUserAsync("Scout", _userId, Arg.Any<CancellationToken>())
            .Returns(new AgentResponse(Guid.NewGuid(), "Scout", "Finds things.", "", DateTime.UtcNow, DateTime.UtcNow));

        var offered = await OfferedToolsAsync("Scout", ct);

        offered.Should().HaveCount(4);
    }

    [Fact]
    public async Task BuiltIn_WithoutOverride_KeepsItsCodeDefinedTools()
    {
        var ct = TestContext.Current.CancellationToken;

        var offered = await OfferedToolsAsync(AgentRole.ResearchAnalyst, ct);

        offered.Should().BeEquivalentTo(ToolNames.KnowledgeBaseSearch, ToolNames.RecallLearnings, ToolNames.FindAssets);
    }

    [Fact]
    public async Task BuiltIn_WithOverride_UsesTheUsersList()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.ToolOverrides.GetAsync(AgentRole.ResearchAnalyst, _userId, Arg.Any<CancellationToken>())
            .Returns(new AgentToolOverrideRecord(AgentRole.ResearchAnalyst, [ToolNames.GetTimelineOverview]));

        var offered = await OfferedToolsAsync(AgentRole.ResearchAnalyst, ct);

        offered.Should().Equal(ToolNames.GetTimelineOverview);
    }

    [Fact]
    public async Task BuiltIn_OverrideToNull_GivesEveryTool()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.ToolOverrides.GetAsync(AgentRole.ResearchAnalyst, _userId, Arg.Any<CancellationToken>())
            .Returns(new AgentToolOverrideRecord(AgentRole.ResearchAnalyst, null));

        var offered = await OfferedToolsAsync(AgentRole.ResearchAnalyst, ct);

        offered.Should().HaveCount(4);
    }

    [Fact]
    public async Task BuiltInAgentService_ReportsDefaultsEffectiveToolsAndCustomisation()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.ToolOverrides.GetAllAsync(_userId, Arg.Any<CancellationToken>())
            .Returns([new AgentToolOverrideRecord(AgentRole.ResearchAnalyst, [ToolNames.FindAssets])]);
        var service = new BuiltInAgentService([new Sentium.Praxis.Features.Agents.Native.GeneralAssistant(), new Sentium.Praxis.Features.Agents.Native.ResearchAnalyst(), new Sentium.Praxis.Features.Agents.Native.OrchestratorAgent()], _host.ToolOverrides);

        var agents = await service.GetAsync(_userId, ct);

        agents.Select(a => a.Name).Should().Equal(AgentRole.GeneralAssistant, AgentRole.ResearchAnalyst);
        agents[0].Should().BeEquivalentTo(new { DefaultTools = (IReadOnlyList<string>?)null, Tools = (IReadOnlyList<string>?)null, IsCustomized = false });
        agents[1].IsCustomized.Should().BeTrue();
        agents[1].Tools.Should().Equal(ToolNames.FindAssets);
        agents[1].DefaultTools.Should().Contain(ToolNames.KnowledgeBaseSearch);
    }

    [Fact]
    public async Task BuiltInAgentService_RejectsControlRoles()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = new BuiltInAgentService([new Sentium.Praxis.Features.Agents.Native.OrchestratorAgent()], _host.ToolOverrides);

        var result = await service.SetToolsAsync(AgentRole.Orchestrator, _userId, [ToolNames.FindAssets], ct);

        result.Status.Should().Be(Sentium.Core.Results.ResultStatus.NotFound);
        await _host.ToolOverrides.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default, default, ct);
    }

    private async Task<List<string>> OfferedToolsAsync(string agentName, CancellationToken ct)
    {
        var agent = await _host.CreateFactory().CreateAsync(agentName, actingUserId: _userId, ct: ct);
        await agent.RunAsync("hi", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        var options = _host.Model.Requests.Should().ContainSingle().Subject.Options!;
        return [.. options.Tools!.Select(t => t.Name).Where(n => n is not ("load_skill" or "read_skill_resource" or "run_skill_script"))];
    }
}
