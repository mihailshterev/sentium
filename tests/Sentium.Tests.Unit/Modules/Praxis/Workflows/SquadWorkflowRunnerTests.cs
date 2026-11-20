using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Praxis.Features.Orchestration;
using Sentium.Praxis.Features.Workflows;
using Sentium.Praxis.Features.Workflows.Squad;
using Xunit;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Sentium.Tests.Unit.Modules.Praxis.Workflows;

/// <summary>
/// The squad graph (planner → step → check → synthesis) driven by scripted models: constrained plans, single-message
/// hand-offs, per-step review with one bounded retry, and the stuck guard.
/// </summary>
public sealed class SquadWorkflowRunnerTests
{
    private const string Research = AgentRole.ResearchAnalyst;
    private const string Engineer = AgentRole.SoftwareEngineer;

    private static readonly SquadMember[] Roster =
    [
        new(AgentRole.GeneralAssistant, "Answers general questions.", "You are a helpful assistant."),
        new(Research, "Finds and summarises information.", "You are a Research Analyst."),
        new(Engineer, "Writes and runs code.", "You are a Software Engineer."),
    ];

    private readonly ScriptedAgentFactory _factory = new();
    private readonly IAgentStreamPublisher _stream = Substitute.For<IAgentStreamPublisher>();
    private readonly WorkflowTrigger _trigger = new() { TriggerType = WorkflowTriggerTypes.Dynamic, StreamId = "s1", UserId = Guid.NewGuid() };

    [Fact]
    public async Task Plan_IsConstrainedToTheRoster_StepsGetOneCompactMessage_AndResultsAreSynthesized()
    {
        var ct = TestContext.Current.CancellationToken;
        _factory.Planner = _ => Plan((Research, "Find the runbook.", "Runbook steps."), (Engineer, "Script it.", "A script."));
        _factory.Specialist = (agent, _) => agent == Research ? "RUNBOOK: deploy, verify." : "SCRIPT: ./deploy.sh";
        _factory.Synthesizer = _ => "Final: runbook + script";
        _factory.Assessor = _ => """{"risk":"Low","recommendation":"Ship it."}""";

        var result = await RunAsync(fixedPlan: null, ct);

        Assert.Equal("Final: runbook + script", result.Answer);
        Assert.Equal(RiskLevel.Low, result.Risk);
        Assert.Equal("Ship it.", result.Recommendation);
        Assert.Equal(new (string Agent, bool Verified)[] { (Research, true), (Engineer, true) }, result.Steps.Select(s => (s.Step.Agent, s.Verified)));

        // The planner may only name roster agents: constrained decoding via a JSON-schema enum.
        var planFormat = Assert.IsType<ChatResponseFormatJson>(_factory.CallsTo(AgentRole.Orchestrator).Single().Options!.ResponseFormat);
        Assert.Equivalent(Roster.Select(m => m.Name), planFormat.Schema!.Value.GetProperty("properties").GetProperty("steps").GetProperty("items").GetProperty("properties")
            .GetProperty("agent").GetProperty("enum").EnumerateArray().Select(e => e.GetString()));

        // Each step sees one user message, never a shared multi-agent transcript; later steps get earlier results.
        var engineerInput = Assert.Single(_factory.CallsTo(Engineer).Single().Messages, m => m.Role == ChatRole.User).Text;
        Assert.Contains("## Results from earlier steps", engineerInput, StringComparison.Ordinal);
        Assert.Contains("RUNBOOK: deploy, verify.", engineerInput, StringComparison.Ordinal);
        Assert.Equal(1, _factory.CallsTo(Research).Single().Messages.Count(m => m.Role == ChatRole.User));

        // Specialists in a team run never get approval-gated tools (nobody can approve them).
        Assert.All(_factory.Created.Where(c => !c.Control), c => Assert.True(!c.AllowApprovalTools));
    }

    [Fact]
    public async Task FailedReview_RetriesTheStepOnceWithTheFixes_ThenAdvances()
    {
        var ct = TestContext.Current.CancellationToken;
        var reviews = 0;
        _factory.Planner = _ => Plan((Research, "Summarise the runbook.", "Steps with sources."));
        _factory.Specialist = (_, attempt) => attempt == 1 ? "steps without sources" : "steps [source: runbook.md]";
        _factory.Reviewer = _ => ++reviews == 1 ? """{"passed":false,"issues":["Cite the source file."]}""" : """{"passed":true,"issues":[]}""";
        _factory.Assessor = _ => """{"risk":"Low","recommendation":"Fine."}""";

        var result = await RunAsync(fixedPlan: null, ct);

        var attempts = _factory.CallsTo(Research);
        Assert.Equal(2, attempts.Count);
        var actual = attempts[1].Messages.Single(m => m.Role == ChatRole.User).Text;
        Assert.Contains("Reviewer feedback", actual, StringComparison.Ordinal);
        Assert.Contains("Cite the source file.", actual, StringComparison.Ordinal);
        Assert.Contains("steps without sources", actual, StringComparison.Ordinal);

        // a single-step plan needs no synthesis call
        Assert.Equal("steps [source: runbook.md]", result.Answer);
        Assert.True(Assert.Single(result.Steps).Verified);
        Assert.Empty(_factory.CallsTo(AgentRole.Summarizer));
    }

    [Fact]
    public async Task RepeatedOutput_IsAcceptedUnverified_WithoutFurtherRetries()
    {
        var ct = TestContext.Current.CancellationToken;
        _factory.Planner = _ => Plan((Research, "Summarise.", "A summary."));
        _factory.Specialist = (_, _) => "the same answer every time";
        _factory.Reviewer = _ => """{"passed":false,"issues":["Be more specific."]}""";
        _factory.Assessor = _ => """{"risk":"Medium","recommendation":"Check it."}""";

        var result = await RunAsync(fixedPlan: null, ct);

        Assert.Equal(SquadWorkflowRunner.MaxAttemptsPerStep, _factory.CallsTo(Research).Count);
        Assert.False(Assert.Single(result.Steps).Verified);
    }

    [Fact]
    public async Task UnreadablePlan_FallsBackToASingleGeneralStep()
    {
        var ct = TestContext.Current.CancellationToken;
        _factory.Planner = _ => "not json at all";
        _factory.Specialist = (_, _) => "general answer";
        _factory.Assessor = _ => """{"risk":"Low","recommendation":"ok"}""";

        var result = await RunAsync(fixedPlan: null, ct);

        Assert.Equal(AgentRole.GeneralAssistant, Assert.Single(result.Steps).Step.Agent);
        Assert.Equal("general answer", result.Answer);
    }

    [Fact]
    public async Task FixedPlan_SkipsThePlanner()
    {
        var ct = TestContext.Current.CancellationToken;
        _factory.Specialist = (agent, _) => $"{agent} part";
        _factory.Synthesizer = _ => "combined";
        _factory.Assessor = _ => """{"risk":"High","recommendation":"Review carefully."}""";

        var plan = new SquadPlan([new SquadStep(Research, "Part A", "A"), new SquadStep(Engineer, "Part B", "B")]);
        var result = await RunAsync(plan, ct);

        Assert.Empty(_factory.CallsTo(AgentRole.Orchestrator));
        Assert.Equal("combined", result.Answer);
        Assert.Equal(RiskLevel.High, result.Risk);
    }

    private Task<SquadResult> RunAsync(SquadPlan? fixedPlan, CancellationToken ct) =>
        new SquadWorkflowRunner(_factory).RunAsync(_trigger, "Deploy the service", Roster, fixedPlan, new WorkflowTimeline(_stream, _trigger.StreamId), ct);

    private static string Plan(params (string Agent, string Task, string Expected)[] steps) =>
        JsonSerializer.Serialize(new { steps = steps.Select(s => new { agent = s.Agent, task = s.Task, expectedOutput = s.Expected }) });

    /// <summary>
    /// Hands out real <see cref="ChatClientAgent"/>s whose model is a per-agent script, and records every model call.
    /// </summary>
    private sealed class ScriptedAgentFactory : IAgentFactory
    {
        private readonly Dictionary<string, int> _attempts = new(StringComparer.OrdinalIgnoreCase);

        public Func<IReadOnlyList<ChatMessage>, string> Planner { get; set; } = _ => """{"steps":[]}""";
        public Func<string, int, string> Specialist { get; set; } = (_, _) => "done";
        public Func<IReadOnlyList<ChatMessage>, string> Reviewer { get; set; } = _ => """{"passed":true,"issues":[]}""";
        public Func<IReadOnlyList<ChatMessage>, string> Synthesizer { get; set; } = _ => "synthesis";
        public Func<IReadOnlyList<ChatMessage>, string> Assessor { get; set; } = _ => """{"risk":"Low","recommendation":"ok"}""";

        public List<(string Agent, List<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];
        public List<(string Agent, bool Control, bool AllowApprovalTools)> Created { get; } = [];

        public List<(string Agent, List<ChatMessage> Messages, ChatOptions? Options)> CallsTo(string agent) =>
            [.. Calls.Where(c => c.Agent == agent)];

        public Task<AIAgent> CreateAsync(string agentName, string? overrideInstructions = null, string? overrideModel = null, Guid? actingUserId = null, ChatHistoryProvider? chatHistory = null, bool allowApprovalTools = true, CancellationToken ct = default)
        {
            Created.Add((agentName, false, allowApprovalTools));
            return Task.FromResult(Build(agentName, overrideInstructions, (messages, _) =>
            {
                _attempts[agentName] = _attempts.GetValueOrDefault(agentName) + 1;
                return Specialist(agentName, _attempts[agentName]);
            }));
        }

        public Task<AgentBlueprint?> ResolveBlueprintAsync(string agentName, string? overrideModel, Guid? actingUserId, bool allowApprovalTools, CancellationToken ct = default)
            => throw new NotSupportedException("Squad workflows build agents, never blueprints.");

        public Task<string> ResolveModelAsync(string? overrideModel, Guid? actingUserId, CancellationToken ct = default)
            => throw new NotSupportedException("Squad workflows build agents, never resolve models.");

        public Task<AIAgent> CreateControlAgentAsync(string agentName, string? overrideInstructions = null, string? overrideModel = null, Guid? actingUserId = null, CancellationToken ct = default)
        {
            Created.Add((agentName, true, false));
            Func<IReadOnlyList<ChatMessage>, ChatOptions?, string> reply = agentName switch
            {
                AgentRole.Orchestrator => (m, _) => Planner(m),
                AgentRole.Summarizer => (m, _) => Synthesizer(m),
                // The final assessment reuses the Validator with replacement instructions; step reviews use its own.
                AgentRole.Validator when overrideInstructions is not null => (m, _) => Assessor(m),
                AgentRole.Validator => (m, _) => Reviewer(m),
                _ => throw new InvalidOperationException($"Unexpected control agent {agentName}."),
            };

            return Task.FromResult(Build(agentName, overrideInstructions, reply));
        }

#pragma warning disable CA2000 // The agent owns its scripted client for the lifetime of the test.
        private AIAgent Build(string name, string? instructions, Func<IReadOnlyList<ChatMessage>, ChatOptions?, string> reply)
        {
            return new ChatClientAgent(new ScriptedClient(name, this, reply), new ChatClientAgentOptions
            {
                Name = name,
                ChatOptions = new ChatOptions { Instructions = instructions },
            });
        }
#pragma warning restore CA2000

        private sealed class ScriptedClient(string agent, ScriptedAgentFactory owner, Func<IReadOnlyList<ChatMessage>, ChatOptions?, string> reply) : IChatClient
        {
            public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            {
                var list = messages.ToList();
                owner.Calls.Add((agent, list, options?.Clone()));
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply(list, options))));
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
    }
}
