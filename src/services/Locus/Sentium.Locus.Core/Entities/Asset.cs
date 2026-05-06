namespace Sentium.Locus.Core.Entities;

/// <summary>
/// Represents any tracked item in the home — from smart appliances to fire extinguishers
/// or important documents. The core building block of the Digital Twin inventory.
/// </summary>
public sealed class Asset
{
    /// <summary>Gets or sets the unique identifier for this asset.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the human-readable name of the asset (e.g., "Main Router", "Fire Extinguisher").</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the category of the asset (e.g., "Appliances", "Documents", "Furniture", "Safety Equipment").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Gets or sets a physical description helping locate the asset
    /// (e.g., "Under the sink, left side", "Top shelf, blue binder").
    /// </summary>
    public string? PhysicalDescription { get; set; }

    /// <summary>
    /// Gets or sets freeform manual notes from the user
    /// (e.g., operating instructions, maintenance history, access codes).
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>Gets or sets the manufacturer or brand name of the asset.</summary>
    public string? Manufacturer { get; set; }

    /// <summary>Gets or sets the model number or product name of the asset.</summary>
    public string? ModelNumber { get; set; }

    /// <summary>Gets or sets the serial number of the asset, if applicable.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Gets or sets the date the asset was purchased.</summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>Gets or sets the date the asset was last serviced or maintained.</summary>
    public DateTime? LastServicedDate { get; set; }

    /// <summary>
    /// Gets or sets warranty information (e.g., expiry date, provider, claim reference).
    /// Supports free-text for maximum flexibility.
    /// </summary>
    public string? WarrantyInfo { get; set; }

    /// <summary>
    /// Gets or sets whether agents are permitted to access and reason about this asset.
    /// Acts as a privacy toggle — set to <c>false</c> to hide the asset from AI context.
    /// </summary>
    public bool IsAgentAccessible { get; set; } = true;

    /// <summary>
    /// Gets or sets specific instructions for agents interacting with this asset
    /// (e.g., "Never suggest moving this", "Always check warranty before advising repair").
    /// </summary>
    public string? AgentInstructions { get; set; }

    /// <summary>Gets or sets the identifier of the location where this asset resides.</summary>
    public Guid LocationId { get; set; }

    /// <summary>Gets or sets when this asset record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when this asset record was last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets or sets the location where this asset resides.</summary>
    public Location Location { get; set; } = null!;
}
