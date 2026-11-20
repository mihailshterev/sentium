using System.ComponentModel;
using Microsoft.Extensions.AI;
using NSubstitute;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Agents.Runtime;
using Sentium.Sentinel.Contracts;
using Sentium.Tests.Unit.Modules.Praxis.Support;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

/// <summary>
/// An approval-gated tool in a Schedule's run, through the real agent pipeline: function invocation, approvals and the
/// Sentinel middleware, as a workflow step builds its agent.
/// </summary>
public sealed class UnattendedAgentRunTests
{
    private const string SchedulePrompt = "Run the nightly clean-up script";

    private readonly AgentTestHost _host = new();
    private readonly GatedCodeTool _tool = new();

    public UnattendedAgentRunTests()
    {
        _host.RunContext.UserId = Guid.NewGuid();
        _host.RunContext.OriginalUserPrompt = SchedulePrompt;
        _host.Tools.Add(_tool.CreateFunction());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GatedTool_WithoutPreApproval_IsDenied_AndTheRunCompletes()
    {
        _host.RunContext.Unattended = new UnattendedRunPolicy(new HashSet<string>());

        var response = await RunCallingTheToolAsync();

        Assert.Equal(0, _tool.Invocations);
        Assert.Equal(UnattendedToolGate.DenialFor(GatedCodeTool.Name), ToolResult());
        Assert.DoesNotContain(response.Messages.SelectMany(m => m.Contents), c => c is ToolApprovalRequestContent);
        Assert.Equal("ok", response.Text);
    }

    [Fact]
    public async Task GatedTool_WithPreApproval_Runs_AfterSentinelChecksItAgainstTheSchedulesPrompt()
    {
        _host.RunContext.Unattended = new UnattendedRunPolicy(new HashSet<string> { GatedCodeTool.Name });

        await RunCallingTheToolAsync();

        Assert.Equal(1, _tool.Invocations);
        Assert.Equal("ran", ToolResult());
        await _host.Pdp.Received(1).EvaluateAsync(
            Arg.Is<PolicyEvaluationRequest>(r => r.SkillName == GatedCodeTool.Name && r.OriginalUserPrompt == SchedulePrompt),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreApprovedTool_IsStillDenied_WhenSentinelDeniesIt()
    {
        _host.RunContext.Unattended = new UnattendedRunPolicy(new HashSet<string> { GatedCodeTool.Name });
        _host.AllowAllTools(allowed: false, reason: "code execution is locked down");

        await RunCallingTheToolAsync();

        Assert.Equal(0, _tool.Invocations);
        Assert.Contains("[Access Denied by Security Policy] code execution is locked down", ToolResult(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowStartedFromThePortal_StillDropsGatedTools()
    {
        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, allowApprovalTools: false, ct: Ct);
        await agent.RunAsync("hello", await agent.CreateSessionAsync(Ct), cancellationToken: Ct);

        Assert.DoesNotContain(_host.Model.Requests[0].Options!.Tools ?? [], t => t.Name == GatedCodeTool.Name);
    }

    private async Task<Microsoft.Agents.AI.AgentResponse> RunCallingTheToolAsync()
    {
        _host.Model.Script(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call-1", GatedCodeTool.Name, new Dictionary<string, object?> { ["code"] = "rm -rf /tmp/cache" })])));

        var agent = await _host.CreateFactory().CreateAsync(AgentRole.GeneralAssistant, allowApprovalTools: false, ct: Ct);
        return await agent.RunAsync(SchedulePrompt, await agent.CreateSessionAsync(Ct), cancellationToken: Ct);
    }

    private string? ToolResult()
    {
        return Assert.Single(_host.Model.Requests[^1].Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>()).Result?.ToString();
    }

    [AgentToolPolicy(RiskLevel = ToolRiskLevel.High, RequiresApproval = true, Resource = ToolResource.Code, Action = ToolAction.Execute)]
    private sealed class GatedCodeTool : IAgentTool
    {
        public const string Name = "gated_code";

        public int Invocations { get; private set; }

        public AIFunction CreateFunction() => AgentToolFunction.Create(this, Execute, Name, "Runs code.");

        private string Execute([Description("Code to run.")] string code)
        {
            Invocations++;
            return "ran";
        }
    }
}
