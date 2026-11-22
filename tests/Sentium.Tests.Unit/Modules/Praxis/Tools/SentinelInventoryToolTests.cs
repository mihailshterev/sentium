using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Locus.Contracts;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Agents;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Praxis.Features.Tools;
using Sentium.Praxis.Features.Tools.Inventory;
using Sentium.Sentinel.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

/// <summary>
/// The inventory tools must reach Sentinel as read/search operations on the Inventory resource: an unclassified
/// tool would bypass the policy check, and a Write/Execute one would forward its arguments as an egress payload.
/// </summary>
public sealed class SentinelInventoryToolTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IPolicyDecisionPoint _pdp = Substitute.For<IPolicyDecisionPoint>();
    private PolicyEvaluationRequest? _captured;

    public SentinelInventoryToolTests()
    {
        _pdp.EvaluateAsync(Arg.Do<PolicyEvaluationRequest>(r => _captured = r), Arg.Any<CancellationToken>())
            .Returns(new PolicyEvaluationResponse
            {
                Allowed = true,
                Effect = PolicyEffect.Allow,
                Reason = "ok",
                Risk = PolicyRiskLevel.Low,
                AuditId = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    private static AIFunction CreateFunction(string toolName)
    {
        var inventory = Substitute.For<IInventoryLookup>();
        IAgentTool[] tools =
        [
            new FindAssetsTool(inventory, NullLogger<FindAssetsTool>.Instance),
            new GetAssetTool(inventory, NullLogger<GetAssetTool>.Instance),
            new ListLocationContentsTool(inventory, NullLogger<ListLocationContentsTool>.Instance),
        ];

        return tools.Select(t => t.CreateFunction()).Single(f => f.Name == toolName);
    }

    [Theory]
    [InlineData(ToolNames.FindAssets, "query", "cordless drill", "Search")]
    [InlineData(ToolNames.GetAsset, "asset", "Cordless drill", "Read")]
    [InlineData(ToolNames.ListLocationContents, "location", "Garage", "Read")]
    public async Task InventoryTools_AreClassifiedAsInventoryReads(string toolName, string argument, string value, string expectedAction)
    {
        var middleware = new SentinelFunctionMiddleware(
            _pdp,
            new PdpContextAccessor(),
            NullLogger<SentinelFunctionMiddleware>.Instance);

        var context = new FunctionInvocationContext
        {
            Function = CreateFunction(toolName),
            Arguments = new AIFunctionArguments { [argument] = value },
        };

        var result = await middleware.InvokeAsync(
            new ChatClientAgent(Substitute.For<IChatClient>(), name: AgentRole.GeneralAssistant),
            context,
            (_, _) => ValueTask.FromResult<object?>("result"),
            Ct);

        Assert.Equal("result", result);
        Assert.NotNull(_captured);
        Assert.Equal(toolName, _captured!.SkillName);
        Assert.Equal(ResourceType.Inventory, _captured.ResourceType);
        Assert.Equal(expectedAction, _captured.Action);
        Assert.Equal(value, _captured.ResourceId);
        // reads and searches never forward an egress payload
        Assert.Empty(_captured.Metadata);
    }
}
