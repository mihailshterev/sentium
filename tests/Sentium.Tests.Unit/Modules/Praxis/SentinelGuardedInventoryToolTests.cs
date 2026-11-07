using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sentium.Praxis.Features.Sentinel;
using Sentium.Sentinel.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

/// <summary>
/// The inventory tools must reach Sentinel as read/search operations on the Inventory resource: an unmapped
/// tool would be classified Any/Execute and its arguments forwarded as an egress payload.
/// </summary>
public sealed class SentinelGuardedInventoryToolTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IPolicyDecisionPoint _pdp = Substitute.For<IPolicyDecisionPoint>();
    private PolicyEvaluationRequest? _captured;

    public SentinelGuardedInventoryToolTests()
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

    private SentinelGuardedAIFunction Guard(string toolName) => new(
        AIFunctionFactory.Create((string? query = null, string? asset = null, string? location = null) => "result", toolName),
        new SentinelClient(_pdp, Substitute.For<ILogger<SentinelClient>>()),
        Substitute.For<IPdpContextAccessor>(),
        "GeneralAssistant",
        Substitute.For<ILogger>());

    [Theory]
    [InlineData("find_assets", "query", "cordless drill", "Search")]
    [InlineData("get_asset", "asset", "Cordless drill", "Read")]
    [InlineData("list_location_contents", "location", "Garage", "Read")]
    public async Task InventoryTools_AreClassifiedAsInventoryReads(string toolName, string argument, string value, string expectedAction)
    {
        var result = await Guard(toolName).InvokeAsync(new AIFunctionArguments { [argument] = value }, Ct);

        result?.ToString().Should().Be("result");
        _captured.Should().NotBeNull();
        _captured!.ResourceType.Should().Be("Inventory");
        _captured.Action.Should().Be(expectedAction);
        _captured.ResourceId.Should().Be(value);
        _captured.Metadata.Should().BeEmpty("reads and searches never forward an egress payload");
    }
}
