namespace Sentium.Locus.Core.Entities;

/// <summary>
/// Models a hierarchical spatial tree (e.g. Home -> Room -> Cabinet).
/// </summary>
public sealed class Location
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? AccessNotes { get; set; }

    /// <summary>
    /// Enables recursive nesting for spatial modeling.
    /// </summary>
    public Guid? ParentLocationId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Location? ParentLocation { get; set; }
    public ICollection<Location> SubLocations { get; set; } = [];
    public ICollection<Asset> Assets { get; set; } = [];
}
