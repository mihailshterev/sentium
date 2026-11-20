using NSubstitute;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Native;
using Sentium.Praxis.Features.Agents.Runtime;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class AgentRosterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IAgentRepository _repository = Substitute.For<IAgentRepository>();

    private AgentRoster CreateRoster()
    {
        return new(
            [new FakeAgent(AgentRole.Orchestrator), new FakeAgent(AgentRole.Validator), new FakeAgent(AgentRole.Summarizer), new FakeAgent(AgentRole.ResearchAnalyst), new GeneralAssistant(), new TimelinePlanner(), new TimelinePlanner()],
            _repository);
    }

    private static AgentResponse Custom(string name, string model = "") => new(Guid.NewGuid(), name, "Mine", model, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Get_ListsTheSpecialists_AndTheUsersOwnAgents()
    {
        _repository.GetAgentsForUserAsync(UserId, Ct).Returns([Custom("Zed"), Custom("Budget Bot", "qwen3"), Custom("timelineplanner")]);

        var roster = await CreateRoster().GetAsync(UserId, Ct);

        Assert.Equal(new string[] { AgentRole.GeneralAssistant, AgentRole.ResearchAnalyst, AgentRole.TimelinePlanner, "Budget Bot", "Zed" }, roster.Select(r => r.Name));
        Assert.Equal(new AgentRosterEntry("Budget Bot", "Mine", AgentKind.Custom, "qwen3"), roster.Single(r => r.Name == "Budget Bot"));
        Assert.Equal(AgentKind.BuiltIn, roster.Single(r => r.Name == AgentRole.TimelinePlanner).Kind);
    }

    [Fact]
    public async Task Find_ReturnsTheCanonicalBuiltInName()
    {
        var entry = await CreateRoster().FindAsync(" timelineplanner ", UserId, Ct);

        Assert.Equal(AgentRole.TimelinePlanner, entry!.Name);
        await _repository.DidNotReceiveWithAnyArgs().GetAgentByNameForUserAsync(default!, default, Ct);
    }

    [Fact]
    public async Task Find_NeverReturnsAControlRole()
    {
        Assert.Null(await CreateRoster().FindAsync("orchestrator", UserId, Ct));
        await _repository.DidNotReceiveWithAnyArgs().GetAgentByNameForUserAsync(default!, default, Ct);
    }

    [Fact]
    public async Task Find_LooksUpCustomAgents_ForTheGivenUserOnly()
    {
        _repository.GetAgentByNameForUserAsync("budget bot", UserId, Ct).Returns(Custom("Budget Bot"));

        Assert.Equal("Budget Bot", (await CreateRoster().FindAsync("budget bot", UserId, Ct))!.Name);
        Assert.Null(await CreateRoster().FindAsync("someone else's", UserId, Ct));
    }

    private sealed class FakeAgent(string name) : IAgent
    {
        public string Name => name;
        public string Instructions => $"{name} instructions";
    }
}
