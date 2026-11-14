using System.ComponentModel;
using FluentAssertions;
using Microsoft.Extensions.AI;
using NSubstitute;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Learnings;
using Sentium.Sentinel.Contracts;
using Sentium.Tests.Unit.Modules.Praxis.Support;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

/// <summary>
/// Pins down the request an agent actually sends to the model: one system prompt (via ChatOptions.Instructions,
/// which the OpenAI-compatible client maps to a single leading system message), harness before role, learnings
/// appended, and the per-agent model carried on the options - plus the Sentinel tool guard.
/// </summary>
public sealed class CompositeAgentFactoryTests
{
    private readonly AgentTestHost _host = new();

    [Fact]
    public async Task Agent_SendsSingleComposedSystemPrompt_HarnessFirst_ThenRole_ThenLearnings()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        _host.RunContext.UserId = userId;
        _host.Learnings.RecallRelevantAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new RecalledLearning("Prefer pgvector HNSW indexes.", 0.9f, "ResearchAnalyst")]);

        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, ct: ct);
        await agent.RunAsync("How should I index vectors?", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        var request = _host.Model.Requests.Should().ContainSingle().Subject;

        request.Messages.Should().NotContain(m => m.Role == ChatRole.System, "the whole system prompt travels as ChatOptions.Instructions");

        var instructions = request.Options!.Instructions;
        instructions.Should().StartWith("### OPERATING PROTOCOL");

        var harness = instructions!.IndexOf("### USER-DEFINED BEHAVIOUR", StringComparison.Ordinal);
        var role = instructions.IndexOf("### AGENT ROLE", StringComparison.Ordinal);

        // The harness policy itself mentions the heading (rule 5), so locate the injected block by its last occurrence.
        var learnings = instructions.LastIndexOf("### RELEVANT PRIOR LEARNINGS", StringComparison.Ordinal);

        harness.Should().BePositive();
        role.Should().BeGreaterThan(harness);
        learnings.Should().BeGreaterThan(role);
        instructions.Should().Contain("Prefer pgvector HNSW indexes.");

        await _host.Learnings.Received(1).RecallRelevantAsync("How should I index vectors?", userId, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Agent_CarriesModel_SizesCompactionForIt_AndSkillToolsNeedNoApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.RunContext.UserId = Guid.NewGuid();

        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, overrideModel: "qwen-override", ct: ct);
        await agent.RunAsync("hi", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        var options = _host.Model.Requests.Should().ContainSingle().Subject.Options!;

        options.ModelId.Should().Be("qwen-override");
        options.AdditionalProperties.Should().BeNullOrEmpty("the context size is fixed per model by the inference server, not sent per request");
        await _host.ContextWindows.Received().GetContextWindowAsync("qwen-override", Arg.Any<CancellationToken>());

        var loadSkill = options.Tools.Should().ContainSingle(t => t.Name == "load_skill").Subject;
        loadSkill.GetService<ApprovalRequiredAIFunction>().Should().BeNull("built-in and user skills are trusted; approval would stall workflows");
    }

    [Fact]
    public async Task Agent_WithoutOverrideOrRegistryModel_UsesTheConfiguredDefaultModel()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.RunContext.UserId = Guid.NewGuid();

        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, ct: ct);
        await agent.RunAsync("hi", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        _host.Model.Requests.Should().ContainSingle().Subject.Options!.ModelId.Should().Be(_host.Inference.DefaultModel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolCalls_AreGuardedBySentinelMiddleware(bool allowed)
    {
        var ct = TestContext.Current.CancellationToken;
        _host.RunContext.UserId = Guid.NewGuid();

        var tool = new GuardedWriteTool();
        _host.Tools.Add(tool.CreateFunction());
        _host.AllowAllTools(allowed, allowed ? "ok" : "egress blocked");
        _host.Model.Script(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call-1", "guarded_write", new Dictionary<string, object?> { ["content"] = "quarterly numbers" })])));

        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, ct: ct);
        await agent.RunAsync("save it", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        tool.Invocations.Should().Be(allowed ? 1 : 0);

        var toolResult = _host.Model.Requests[^1].Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Should().ContainSingle().Subject;
        if (allowed)
        {
            toolResult.Result?.ToString().Should().Be("saved");
        }
        else
        {
            toolResult.Result?.ToString().Should().Contain("[Access Denied by Security Policy] egress blocked");
        }

        await _host.Pdp.Received(1).EvaluateAsync(
            Arg.Is<PolicyEvaluationRequest>(r =>
                r.AgentId == AgentRole.GeneralAssistant && r.SkillName == "guarded_write" &&
                r.ResourceType == "File" && r.Action == "Write" && r.Metadata["payload"].Contains("quarterly numbers")),
            Arg.Any<CancellationToken>());
    }

    [AgentToolPolicy(Resource = ToolResource.File, Action = ToolAction.Write)]
    private sealed class GuardedWriteTool : IAgentTool
    {
        public int Invocations { get; private set; }

        public AIFunction CreateFunction() => AgentToolFunction.Create(this, Write, "guarded_write", "Writes content.");

        private string Write([Description("Content to write.")] string content)
        {
            Invocations++;
            return "saved";
        }
    }
}
