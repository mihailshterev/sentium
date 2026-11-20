using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents.Runtime;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Agents;

public sealed class UnattendedToolGateTests
{
    private const string GatedName = "run_code";

    private int _calls;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GatedTool_WithoutPreApproval_IsDenied_AsAToolResult()
    {
        var tool = Assert.IsAssignableFrom<AIFunction>(UnattendedToolGate.Apply(GatedTool(), Policy()));

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["code"] = "print(1)" }, Ct);

        Assert.Equal(0, _calls);
        Assert.Equal(UnattendedToolGate.DenialFor(GatedName), result);
        Assert.Null(tool.GetService<ApprovalRequiredAIFunction>());
        Assert.Equal(GatedName, tool.Name);
        // the stub never runs the tool, so Sentinel is not asked about it
        Assert.False(AgentToolFunction.TryGetClassification(tool, out _, out _));
    }

    [Fact]
    public async Task GatedTool_WithPreApproval_RunsWithoutApproval_AndKeepsItsSentinelClassification()
    {
        var tool = Assert.IsAssignableFrom<AIFunction>(UnattendedToolGate.Apply(GatedTool(), Policy(GatedName)));

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["code"] = "print(1)" }, Ct);

        Assert.Equal(1, _calls);
        Assert.Equal("ran print(1)", result?.ToString());
        Assert.Null(tool.GetService<ApprovalRequiredAIFunction>());
        Assert.True(AgentToolFunction.TryGetClassification(tool, out var resource, out var action));
        Assert.Equal(nameof(ToolResource.Code), resource);
        Assert.Equal(nameof(ToolAction.Execute), action);
        Assert.Equal(GatedTool().JsonSchema.GetRawText(), tool.JsonSchema.GetRawText());
    }

    [Fact]
    public void UngatedTool_IsLeftAsItIs()
    {
        var tool = AIFunctionFactory.Create(() => "ok", "read_things");

        Assert.Same(tool, UnattendedToolGate.Apply(tool, Policy()));
    }

    private static UnattendedRunPolicy Policy(params string[] preApproved) => new(new HashSet<string>(preApproved, StringComparer.OrdinalIgnoreCase));

    private ApprovalRequiredAIFunction GatedTool()
    {
        var inner = AIFunctionFactory.Create(
            (string code) =>
            {
                _calls++;
                return $"ran {code}";
            },
            new AIFunctionFactoryOptions
            {
                Name = GatedName,
                AdditionalProperties = AgentToolFunction.Classification(ToolResource.Code, ToolAction.Execute),
            });

        return new ApprovalRequiredAIFunction(inner);
    }
}
