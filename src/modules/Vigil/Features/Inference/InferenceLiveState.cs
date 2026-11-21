using Sentium.Infrastructure.Inference;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// The latest model states the collector saw, per server. A server that could not be read drops its models, so
/// nothing stale is shown; the LLM and Embeddings probes report the outage itself.
/// </summary>
internal sealed class InferenceLiveState
{
    private readonly Lock _gate = new();
    private readonly Dictionary<InferenceEndpoint, IReadOnlyList<InferenceModelState>> _byServer = [];

    public void Replace(InferenceEndpoint server, IReadOnlyList<InferenceModelState> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        lock (_gate)
        {
            _byServer[server] = models;
        }
    }

    public void Clear(InferenceEndpoint server)
    {
        lock (_gate)
        {
            _byServer.Remove(server);
        }
    }

    public IReadOnlyList<InferenceModelState> Snapshot()
    {
        lock (_gate)
        {
            return _byServer.OrderBy(s => s.Key).SelectMany(s => s.Value).ToList();
        }
    }
}
