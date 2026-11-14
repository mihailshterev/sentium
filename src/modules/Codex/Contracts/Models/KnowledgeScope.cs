namespace Sentium.Codex.Contracts.Models;

/// <summary>
/// Visibility scope for a knowledge-base entry. The knowledge base is shared across all users,
/// but users may also contribute private entries that only they (and Sovereigns) can retrieve.
/// </summary>
public static class KnowledgeScope
{
    public const string Shared = "shared";
    public const string User = "user";
}
