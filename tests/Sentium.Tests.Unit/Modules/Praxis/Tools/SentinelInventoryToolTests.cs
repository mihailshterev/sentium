using FluentAssertions;
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
                Effect = "Allow",
                Reason = "ok",
                Risk = "Low",
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
            new SentinelClient(_pdp, NullLogger<SentinelClient>.Instance),
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

        result.Should().Be("result");
        _captured.Should().NotBeNull();
        _captured!.SkillName.Should().Be(toolName);
        _captured.ResourceType.Should().Be("Inventory");
        _captured.Action.Should().Be(expectedAction);
        _captured.ResourceId.Should().Be(value);
        _captured.Metadata.Should().BeEmpty("reads and searches never forward an egress payload");
    }
}
