using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sentium.Infrastructure.Inference;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Native;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.Agents.ToolOverrides;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Models;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Skills;
using Sentium.Praxis.Features.Skills.BuiltIn;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Workspaces;
using Sentium.Registry.Contracts;
using Sentium.Sentinel.Contracts;

namespace Sentium.Tests.Unit.Modules.Praxis.Support;

/// <summary>
/// Builds the real <see cref="CompositeAgentFactory"/> (and so the real Agent Framework pipeline) over a scripted
/// <see cref="RecordingChatClient"/> model and substituted data services.
/// </summary>
internal sealed class AgentTestHost
{
    public RecordingChatClient Model { get; } = new();
    public IAgentLearningService Learnings { get; } = Substitute.For<IAgentLearningService>();

    public IWorkspaceContextBuilder WorkspaceContext { get; } = Substitute.For<IWorkspaceContextBuilder>();
    public IAgentSkillRepository Skills { get; } = Substitute.For<IAgentSkillRepository>();
    public IRegistrySettingsService Settings { get; } = Substitute.For<IRegistrySettingsService>();
    public ISettingsService CalendarSettings { get; } = Substitute.For<ISettingsService>();
    public TimeProvider Clock { get; } = Substitute.For<TimeProvider>();
    public IPolicyDecisionPoint Pdp { get; } = Substitute.For<IPolicyDecisionPoint>();
    public PdpContextAccessor RunContext { get; } = new();
    public InferenceOptions Inference { get; } = new() { DefaultModel = "gemma-test" };
    public IModelContextWindowProvider ContextWindows { get; } = Substitute.For<IModelContextWindowProvider>();
    public List<AITool> Tools { get; } = [];
    public IAgentRepository Agents { get; } = Substitute.For<IAgentRepository>();
    public IAgentToolOverrideRepository ToolOverrides { get; } = Substitute.For<IAgentToolOverrideRepository>();
    public List<AIFunction> McpTools { get; } = [];
    /// <summary>
    /// Stands in for the MCP kill switch: while false the MCP source yields no tools.
    /// </summary>
    public bool McpEnabled { get; set; } = true;

    public AgentTestHost()
    {
        Settings.GetAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new SettingsSnapshot(
            new HarnessSettingsSnapshot(UserHarnessPrompt: "Answer in British English.", IsBuiltInHarnessEnabled: true)));
        Skills.GetForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AgentSkill>());
        Learnings.RecallRelevantAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RecalledLearning>());
        ContextWindows.GetContextWindowAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(8192);
        Clock.GetUtcNow().Returns(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero));
        AllowAllTools();
    }

    public void AllowAllTools(bool allowed = true, string reason = "ok")
    {
        Pdp.EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), Arg.Any<CancellationToken>()).Returns(new PolicyEvaluationResponse
        {
            Allowed = allowed,
            Effect = allowed ? PolicyEffect.Allow : PolicyEffect.Deny,
            Reason = reason,
            Risk = PolicyRiskLevel.Low,
            AuditId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>
    /// A fresh factory - as a new request's DI scope would build one - sharing this host's model and data services.
    /// </summary>
    public CompositeAgentFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient<IAgent, GeneralAssistant>(AgentRole.GeneralAssistant);
        services.AddKeyedTransient<IAgent, ResearchAnalyst>(AgentRole.ResearchAnalyst);
        var provider = services.BuildServiceProvider();

        var toolProvider = Substitute.For<IAgentToolProvider>();
        toolProvider.GetToolsForAgent(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => [.. Tools]);

        // Stands in for the MCP source: like the real one it yields nothing while MCP is disabled.
        var mcpSource = Substitute.For<IAgentToolSource>();
        mcpSource.GetFunctionsAsync(Arg.Any<IReadOnlySet<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult<IReadOnlyList<AIFunction>>(
                McpEnabled ? [.. McpTools.Where(t => call.Arg<IReadOnlySet<string>>().Contains(t.Name))] : []));

        return new CompositeAgentFactory(
            Model,
            Options.Create(Inference),
            ContextWindows,
            toolProvider,
            [mcpSource],
            Agents,
            new BuiltInAgentService([new GeneralAssistant(), new ResearchAnalyst()], ToolOverrides),
            Settings,
            new AgentSkillsProviderFactory(new BuiltInSkillCatalog(), Skills, RunContext, NullLoggerFactory.Instance),
            new CurrentDateContextProvider(CalendarSettings, RunContext, Clock, NullLogger<CurrentDateContextProvider>.Instance),
            new LearningsContextProvider(Learnings, RunContext),
            new WorkspaceContextProviderFactory(WorkspaceContext, RunContext),
            new SentinelFunctionMiddleware(Pdp, RunContext, NullLogger<SentinelFunctionMiddleware>.Instance),
            RunContext,
            provider,
            NullLoggerFactory.Instance);
    }
}
