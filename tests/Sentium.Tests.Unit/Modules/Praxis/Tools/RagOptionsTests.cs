using Sentium.Codex.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Tools;

public sealed class RagOptionsTests
{
    [Fact]
    public void SearchCollections_DefaultsToEveryStoreIncludingVaultNotes()
        => Assert.Equivalent(new[] { "knowledge_base", "agent_learnings", "user_memories", "vault_notes" }, new RagOptions().SearchCollections);
}
