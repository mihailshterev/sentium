namespace Sentium.Locus.Core.Entities;

/// <summary>
/// Represents a physical or logical space within the home (e.g., a room, storage area, or zone).
/// Locations form a hierarchical structure to model nested spaces.
/// </summary>
public sealed class Location
{
    /// <summary>Gets or sets the unique identifier for this location.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the display name of the location (e.g., "Master Bedroom", "Basement Storage").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Gets or sets an optional description providing additional context about this location.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets access notes describing how to reach this location
    /// (e.g., "Key is in the kitchen drawer", "Behind the blue cabinet").
    /// </summary>
    public string? AccessNotes { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the parent location, enabling hierarchical nesting
    /// (e.g., "Under Sink" nested inside "Kitchen").
    /// </summary>
    public Guid? ParentLocationId { get; set; }

    /// <summary>Gets or sets when this location record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when this location record was last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets or sets the parent location, if any.</summary>
    public Location? ParentLocation { get; set; }

    /// <summary>Gets or sets the child locations nested under this location.</summary>
    public ICollection<Location> SubLocations { get; set; } = [];

    /// <summary>Gets or sets the assets catalogued within this location.</summary>
    public ICollection<Asset> Assets { get; set; } = [];
}
