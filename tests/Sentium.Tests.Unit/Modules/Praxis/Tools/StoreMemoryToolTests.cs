using Microsoft.Extensions.Logging;
using NSubstitute;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Memories;
using Sentium.Praxis.Features.Tools;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

public sealed class StoreMemoryToolTests
{
    private readonly IUserMemoryService _memories = Substitute.For<IUserMemoryService>();
    private readonly IPdpContextAccessor _pdp = Substitute.For<IPdpContextAccessor>();

    private StoreMemoryTool CreateTool() => new(_memories, _pdp, Substitute.For<ILogger<StoreMemoryTool>>());

    [Fact]
    public async Task WithoutActingUser_RefusesAndStoresNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        _pdp.UserId.Returns((Guid?)null);

        var result = await CreateTool().StoreAsync("My favourite colour is green.", ct);

        Assert.StartsWith("Error:", result, StringComparison.Ordinal);
        await _memories.DidNotReceive().RememberAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithActingUser_StoresForThatUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        _pdp.UserId.Returns(userId);
        _memories.RememberAsync(userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RememberResult(Memory("My favourite colour is green."), ReplacedExisting: false));

        var result = await CreateTool().StoreAsync("My favourite colour is green.", ct);

        Assert.StartsWith("Saved memory", result, StringComparison.Ordinal);
        await _memories.Received(1).RememberAsync(userId, "My favourite colour is green.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestatedFact_ReportsTheReplacedMemory()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        var existing = Memory("My favourite colour is green.");
        _pdp.UserId.Returns(userId);
        _memories.RememberAsync(userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RememberResult(existing, ReplacedExisting: true));

        var result = await CreateTool().StoreAsync("Green is my favourite colour.", ct);

        Assert.Contains($"Updated memory {existing.Id}", result, StringComparison.Ordinal);
    }

    private static UserMemoryDto Memory(string content) => new(Guid.NewGuid(), content, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
