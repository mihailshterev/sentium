namespace Sentium.Praxis.Features.Storage;

/// <summary>
/// A stored file waiting for RAG ingestion (read from blob storage, vectorized, indexed).
/// </summary>
internal sealed record FileIngestionRequest(Guid FileId, Guid? WorkspaceId);
