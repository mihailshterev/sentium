namespace Sentium.Locus.Entities;

/// <summary>
/// A place in the household's spatial tree (e.g. Home → Garage → Shelf B). Locations are shared by every
/// member of the household; sibling names are unique case-insensitively (see <see cref="NormalizedName"/>).
/// </summary>
internal sealed class Location
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Case-folded <see cref="Name"/> backing the unique sibling-name index. Always written through
    /// <see cref="Features.Locations.LocationNames.Normalize"/>, never directly from user input.
    /// </summary>
    public string NormalizedName { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// How to physically get to this place (keys, codes, which drawer sticks). Never shared with
    /// assistants or the knowledge base.
    /// </summary>
    public string? AccessNotes { get; set; }

    public Guid? ParentLocationId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public Location? Parent { get; set; }

    public ICollection<Location> Children { get; set; } = [];

    public ICollection<Asset> Assets { get; set; } = [];
}
