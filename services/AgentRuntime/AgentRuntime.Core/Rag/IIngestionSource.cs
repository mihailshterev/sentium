using AgentRuntime.Core.Rag.Models;

namespace AgentRuntime.Core.Rag;

/// <summary>
/// Pluggable contract for any external data source that can push content into the knowledge base.
/// <para>
/// Implement this interface for each new data origin (e.g. inventory service, Zeek log exporter)
/// and register it with the DI container. The <see cref="IDocumentIngestionService"/> can then
/// drive all registered sources via <c>IngestFromSourceAsync</c>.
/// </para>
/// <example>
/// <code>
/// // In ServiceCollectionExtensions or a module registration:
/// services.AddTransient&lt;IIngestionSource, InventoryIngestionSource&gt;();
/// </code>
/// </example>
/// </summary>
public interface IIngestionSource
{
    /// <summary>
    /// Stable, human-readable name used in source citations (e.g. "inventory-service").
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Enum-typed category written onto every chunk produced by this source.
    /// </summary>
    IngestionSourceType SourceType { get; }

    /// <summary>
    /// Asynchronously yields <see cref="IngestionRequest"/> records to be processed by the ingestion pipeline.
    /// Implementations should stream results rather than buffering the full dataset.
    /// </summary>
    IAsyncEnumerable<IngestionRequest> FetchDocumentsAsync(CancellationToken ct = default);
}
