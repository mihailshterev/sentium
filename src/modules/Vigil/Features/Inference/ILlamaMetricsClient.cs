using Sentium.Infrastructure.Inference;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Reads llama-server's model list and per-model metrics. An unreachable or unexpected server yields
/// <see langword="null"/>; reachability itself is reported by the LLM and Embeddings probes.
/// </summary>
internal interface ILlamaMetricsClient
{
    Task<IReadOnlyList<ServedModel>?> ListModelsAsync(InferenceEndpoint server, CancellationToken ct);

    Task<LlamaServerSnapshot?> GetSnapshotAsync(InferenceEndpoint server, ServedModel model, CancellationToken ct);
}
