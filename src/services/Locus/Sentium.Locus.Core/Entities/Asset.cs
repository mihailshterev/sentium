namespace Sentium.Locus.Core.Entities;

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
    public DateTime? PurchaseDate { get; set; }
    public DateTime? LastServicedDate { get; set; }
    public string? WarrantyInfo { get; set; }

    /// <summary>
    /// Privacy toggle. If false, this asset is excluded from AI RAG/reasoning contexts.
    /// </summary>
    public bool IsAgentAccessible { get; set; } = true;

    /// <summary>
    /// System-prompt injections used by agents when interacting with this specific asset.
    /// </summary>
    public string? AgentInstructions { get; set; }

    public Guid LocationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Location Location { get; set; } = null!;
}
