using System.ComponentModel;
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

        var request = Assert.Single(_host.Model.Requests);

        // the whole system prompt travels as ChatOptions.Instructions
        Assert.DoesNotContain(request.Messages, m => m.Role == ChatRole.System);

        var instructions = request.Options!.Instructions;
        Assert.StartsWith("### OPERATING PROTOCOL", instructions, StringComparison.Ordinal);

        var harness = instructions!.IndexOf("### USER-DEFINED BEHAVIOUR", StringComparison.Ordinal);
        var role = instructions.IndexOf("### AGENT ROLE", StringComparison.Ordinal);

        // The harness policy itself mentions the heading (rule 5), so locate the injected block by its last occurrence.
        var learnings = instructions.LastIndexOf("### RELEVANT PRIOR LEARNINGS", StringComparison.Ordinal);

        Assert.True(harness > 0);
        Assert.True(role > harness);
        Assert.True(learnings > role);
        Assert.Contains("Prefer pgvector HNSW indexes.", instructions, StringComparison.Ordinal);

        await _host.Learnings.Received(1).RecallRelevantAsync("How should I index vectors?", userId, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Agent_CarriesModel_SizesCompactionForIt_AndSkillToolsNeedNoApproval()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.RunContext.UserId = Guid.NewGuid();

        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, overrideModel: "qwen-override", ct: ct);
        await agent.RunAsync("hi", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        var options = Assert.Single(_host.Model.Requests).Options!;

        Assert.Equal("qwen-override", options.ModelId);
        Assert.True(options.AdditionalProperties is null || options.AdditionalProperties.Count == 0, "the context size is fixed per model by the inference server, not sent per request");
        await _host.ContextWindows.Received().GetContextWindowAsync("qwen-override", Arg.Any<CancellationToken>());

        var loadSkill = Assert.Single(options.Tools!, t => t.Name == "load_skill");
        // built-in and user skills are trusted; approval would stall workflows
        Assert.Null(loadSkill.GetService<ApprovalRequiredAIFunction>());
    }

    [Fact]
    public async Task Agent_WithoutOverrideOrRegistryModel_UsesTheConfiguredDefaultModel()
    {
        var ct = TestContext.Current.CancellationToken;
        _host.RunContext.UserId = Guid.NewGuid();

        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, ct: ct);
        await agent.RunAsync("hi", await agent.CreateSessionAsync(ct), cancellationToken: ct);

        Assert.Equal(_host.Inference.DefaultModel, Assert.Single(_host.Model.Requests).Options!.ModelId);
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

        Assert.Equal(allowed ? 1 : 0, tool.Invocations);

        var toolResult = Assert.Single(_host.Model.Requests[^1].Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>());
        if (allowed)
        {
            Assert.Equal("saved", toolResult.Result?.ToString());
        }
        else
        {
            Assert.Contains("[Access Denied by Security Policy] egress blocked", toolResult.Result?.ToString(), StringComparison.Ordinal);
        }

        await _host.Pdp.Received(1).EvaluateAsync(
            Arg.Is<PolicyEvaluationRequest>(r =>
                r.AgentId == AgentRole.GeneralAssistant && r.SkillName == "guarded_write" &&
                r.ResourceType == ResourceType.File && r.Action == "Write" && r.Payload!.Contains("quarterly numbers")),
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
