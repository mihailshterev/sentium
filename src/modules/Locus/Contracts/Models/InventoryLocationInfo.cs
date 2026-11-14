namespace Sentium.Locus.Contracts.Models;

public sealed record InventoryLocationInfo(Guid Id, string Name, string Path, int SubLocationCount);
