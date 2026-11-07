using Sentium.Codex.Contracts.Models;

namespace Sentium.Codex.Contracts;

/// <summary>
/// Pluggable contract for any external data source that can push content into the knowledge base.
/// <para>
/// Implement this interface for each new data origin
/// and register it with the DI container. The <see cref="IDocumentIngestionService"/> can then
/// drive all registered sources via <c>IngestFromSourceAsync</c>.
/// </para>
/// <para>
/// Modules that own their data and know when it changes (e.g. Locus) push content directly through
/// <see cref="IDocumentIngestionService.IngestAsync"/> instead of implementing a pull source.
/// </para>
/// <example>
/// <code>
/// // In a module registration:
/// services.AddTransient&lt;IIngestionSource, MyFeedIngestionSource&gt;();
/// </code>
/// </example>
/// </summary>
public interface IIngestionSource
{
    /// <summary>
    /// Stable, human-readable name used in source citations.
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
