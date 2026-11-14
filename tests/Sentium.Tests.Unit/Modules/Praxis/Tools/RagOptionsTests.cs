using FluentAssertions;
using Sentium.Codex.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

public sealed class RagOptionsTests
{
    [Fact]
    public void SearchCollections_DefaultsToAllThreeStores()
        => new RagOptions().SearchCollections.Should().BeEquivalentTo(new[] { "knowledge_base", "agent_learnings", "user_memories" });
}
