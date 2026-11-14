namespace Sentium.Locus.Features.Locations;

public sealed record LocationDeleteResult(LocationDeleteStatus Status, int ChildCount = 0, int AssetCount = 0, string? Error = null);
