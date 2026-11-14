namespace Sentium.Locus.Features.Locations;

public sealed record LocationNode(Guid Id, Guid? ParentId, string Name);
