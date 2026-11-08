namespace Sentium.Locus.Entities;

/// <summary>
/// A physical item the household owns, kept at exactly one <see cref="Location"/>.
/// </summary>
public sealed class Asset
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? Category { get; set; }

    public string? PhysicalDescription { get; set; }

    public string? Instructions { get; set; }

    public string? Manufacturer { get; set; }

    public string? ModelNumber { get; set; }

    public string? SerialNumber { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    public DateOnly? LastServicedDate { get; set; }

    public string? WarrantyInfo { get; set; }

    /// <summary>
    /// Privacy toggle. When <see langword="false"/> the asset is hidden from assistant tools and removed
    /// from the knowledge base.
    /// </summary>
    public bool IsAgentAccessible { get; set; } = true;

    /// <summary>
    /// Household-written notes for assistants about this asset. Treated as untrusted data, never as
    /// system instructions.
    /// </summary>
    public string? AgentInstructions { get; set; }

    /// <summary>
    /// Normalized (lower-case, trimmed, de-duplicated) tags; see <see cref="Sentium.Core.Text.TagNormalizer"/>.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Lower-cased concatenation of the searchable fields and tags, maintained on every write so search
    /// is one case-insensitive pattern match per term.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    public Guid LocationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Hash of the content last written to the knowledge base; <see langword="null"/> when the asset has
    /// no knowledge-base copy.
    /// </summary>
    public string? KnowledgeContentHash { get; set; }

    public DateTimeOffset? KnowledgeSyncedAt { get; set; }

    public Location Location { get; set; } = null!;

    public ICollection<AssetAttachment> Attachments { get; set; } = [];
}
