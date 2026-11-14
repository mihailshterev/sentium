namespace Sentium.Locus.Contracts.Models;

public enum InventoryLocationLookupStatus
{
    Found,
    NotFound,

    /// <summary>
    /// The reference matched several locations; see <see cref="InventoryLocationLookup.Candidates"/>.
    /// </summary>
    Ambiguous
}
