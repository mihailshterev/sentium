namespace Sentium.Codex.Contracts.Models;

public sealed record CollectionStats(
    string CollectionName,
    long PointCount,
    uint VectorSize,
    string DistanceMetric);
