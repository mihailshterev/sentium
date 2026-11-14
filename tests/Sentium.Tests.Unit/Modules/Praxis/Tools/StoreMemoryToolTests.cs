using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sentium.Codex.Contracts;
using Sentium.Codex.Contracts.Models;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.Tools;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

public sealed class StoreMemoryToolTests
{
    private readonly IDocumentIngestionService _ingestion = Substitute.For<IDocumentIngestionService>();
    private readonly IPdpContextAccessor _pdp = Substitute.For<IPdpContextAccessor>();

    private StoreMemoryTool CreateTool() => new(_ingestion, _pdp, Substitute.For<ILogger<StoreMemoryTool>>());

    [Fact]
    public async Task WithoutActingUser_RefusesAndNeverWritesToTheSharedStore()
    {
        var ct = TestContext.Current.CancellationToken;
        _pdp.UserId.Returns((Guid?)null);

        var result = await CreateTool().StoreAsync("My favourite colour is green.", ct);

        result.Should().StartWith("Error:");
        await _ingestion.DidNotReceive().IngestAsync(Arg.Any<IngestionRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithActingUser_StoresInThatUsersPrivateScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        _pdp.UserId.Returns(userId);

        await CreateTool().StoreAsync("My favourite colour is green.", ct);

        await _ingestion.Received(1).IngestAsync(
            Arg.Is<IngestionRequest>(r => r.Scope == KnowledgeScope.User && r.UserId == userId),
            KnowledgeCollections.UserMemories,
            Arg.Any<CancellationToken>());
    }
}
