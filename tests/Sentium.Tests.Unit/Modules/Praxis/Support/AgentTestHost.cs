using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Native;
using Sentium.Praxis.Features.Learnings;
using Sentium.Praxis.Features.Registry;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Skills;
using Sentium.Praxis.Features.Skills.BuiltIn;
using Sentium.Praxis.Features.Tools;
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
    public IAgentSkillRepository Skills { get; } = Substitute.For<IAgentSkillRepository>();
    public IRegistrySettingsService Settings { get; } = Substitute.For<IRegistrySettingsService>();
    public IPolicyDecisionPoint Pdp { get; } = Substitute.For<IPolicyDecisionPoint>();
    public PdpContextAccessor RunContext { get; } = new();
    public OllamaOptions Ollama { get; } = new() { DefaultModel = "gemma-test", AgentContextWindow = 8192 };
    public List<AITool> Tools { get; } = [];

    public AgentTestHost()
    {
        Settings.GetAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new SettingsSnapshot(
            new HarnessSettingsSnapshot(UserHarnessPrompt: "Answer in British English.", IsBuiltInHarnessEnabled: true)));
        Skills.GetForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AgentSkill>());
        Learnings.RecallRelevantAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RecalledLearning>());
        AllowAllTools();
    }

    public void AllowAllTools(bool allowed = true, string reason = "ok") =>
        Pdp.EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), Arg.Any<CancellationToken>()).Returns(new PolicyEvaluationResponse
        {
            Allowed = allowed,
            Effect = allowed ? "Allow" : "Deny",
            Reason = reason,
            Risk = "Low",
            AuditId = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
        });

    /// <summary>
    /// A fresh factory - as a new request's DI scope would build one - sharing this host's model and data services.
    /// </summary>
    public CompositeAgentFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient<IAgent, GeneralAssistant>(AgentRole.GeneralAssistant);
        var provider = services.BuildServiceProvider();

        var toolProvider = Substitute.For<IAgentToolProvider>();
        toolProvider.GetToolsForAgent(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => [.. Tools]);

        var sentinel = new SentinelClient(Pdp, NullLogger<SentinelClient>.Instance);

        return new CompositeAgentFactory(
            Model,
            Ollama,
            toolProvider,
            Substitute.For<IAgentRepository>(),
            Settings,
            new AgentSkillsProviderFactory(new BuiltInSkillCatalog(), Skills, RunContext, NullLoggerFactory.Instance),
            new LearningsContextProvider(Learnings, RunContext),
            new SentinelFunctionMiddleware(sentinel, RunContext, NullLogger<SentinelFunctionMiddleware>.Instance),
            RunContext,
            provider,
            NullLoggerFactory.Instance);
    }
}

/// <summary>
/// A model stand-in: records every request and replies with queued responses, then "ok".
/// </summary>
internal sealed class RecordingChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _scripted = new();

    public List<(List<ChatMessage> Messages, ChatOptions? Options)> Requests { get; } = [];

    public void Script(ChatResponse response) => _scripted.Enqueue(response);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(([.. messages], options?.Clone()));
        return Task.FromResult(_scripted.TryDequeue(out var next) ? next : new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
