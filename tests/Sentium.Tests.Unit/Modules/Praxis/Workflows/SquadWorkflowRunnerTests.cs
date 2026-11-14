using System.Text.Json;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using NSubstitute;
using Sentium.Praxis.Features.Agents;
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

        result.Answer.Should().Be("Final: runbook + script");
        result.Risk.Should().Be(RiskLevel.Low);
        result.Recommendation.Should().Be("Ship it.");
        result.Steps.Select(s => (s.Step.Agent, s.Verified)).Should().Equal((Research, true), (Engineer, true));

        // The planner may only name roster agents: constrained decoding via a JSON-schema enum.
        var planFormat = _factory.CallsTo(AgentRole.Orchestrator).Single().Options!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>().Subject;
        planFormat.Schema!.Value.GetProperty("properties").GetProperty("steps").GetProperty("items").GetProperty("properties")
            .GetProperty("agent").GetProperty("enum").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo(Roster.Select(m => m.Name));

        // Each step sees one user message, never a shared multi-agent transcript; later steps get earlier results.
        var engineerInput = _factory.CallsTo(Engineer).Single().Messages.Should().ContainSingle(m => m.Role == ChatRole.User).Subject.Text;
        engineerInput.Should().Contain("## Results from earlier steps").And.Contain("RUNBOOK: deploy, verify.");
        _factory.CallsTo(Research).Single().Messages.Count(m => m.Role == ChatRole.User).Should().Be(1);

        // Specialists in a team run never get approval-gated tools (nobody can approve them).
        _factory.Created.Where(c => !c.Control).Should().OnlyContain(c => !c.AllowApprovalTools);
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
        attempts.Should().HaveCount(2);
        attempts[1].Messages.Single(m => m.Role == ChatRole.User).Text
            .Should().Contain("Reviewer feedback").And.Contain("Cite the source file.").And.Contain("steps without sources");

        result.Answer.Should().Be("steps [source: runbook.md]", "a single-step plan needs no synthesis call");
        result.Steps.Should().ContainSingle().Which.Verified.Should().BeTrue();
        _factory.CallsTo(AgentRole.Summarizer).Should().BeEmpty();
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

        _factory.CallsTo(Research).Should().HaveCount(SquadWorkflowRunner.MaxAttemptsPerStep);
        result.Steps.Should().ContainSingle().Which.Verified.Should().BeFalse();
    }

    [Fact]
    public async Task UnreadablePlan_FallsBackToASingleGeneralStep()
    {
        var ct = TestContext.Current.CancellationToken;
        _factory.Planner = _ => "not json at all";
        _factory.Specialist = (_, _) => "general answer";
        _factory.Assessor = _ => """{"risk":"Low","recommendation":"ok"}""";

        var result = await RunAsync(fixedPlan: null, ct);

        result.Steps.Should().ContainSingle().Which.Step.Agent.Should().Be(AgentRole.GeneralAssistant);
        result.Answer.Should().Be("general answer");
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

        _factory.CallsTo(AgentRole.Orchestrator).Should().BeEmpty();
        result.Answer.Should().Be("combined");
        result.Risk.Should().Be(RiskLevel.High);
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
