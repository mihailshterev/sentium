namespace Sentium.Memoria.Features.Notes;

/// <param name="Truncated">More notes matched than the node limit; the best-linked ones are shown.</param>
internal sealed record VaultGraphDto(IReadOnlyList<VaultGraphNodeDto> Nodes, IReadOnlyList<VaultGraphEdgeDto> Edges, bool Truncated);
