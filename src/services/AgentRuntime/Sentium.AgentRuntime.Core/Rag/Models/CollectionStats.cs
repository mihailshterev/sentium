namespace Sentium.AgentRuntime.Core.Rag.Models;

public sealed record CollectionStats(
    string CollectionName,
    long PointCount,
    uint VectorSize,
    string DistanceMetric);
