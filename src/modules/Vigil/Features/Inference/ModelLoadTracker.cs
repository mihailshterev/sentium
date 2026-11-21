using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Entities;

namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Watches router model states for <c>loading</c> and reports how each load ended. A load that starts and ends
/// between two samples is never seen. Not thread-safe: the collector owns it.
/// </summary>
internal sealed class ModelLoadTracker
{
    private readonly Dictionary<(InferenceEndpoint Server, string Model), DateTimeOffset> _loadingSince = [];

    public IReadOnlyList<ModelLoadEvent> Observe(InferenceEndpoint server, IReadOnlyList<ServedModel> models, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(models);

        var completed = new List<ModelLoadEvent>();

        foreach (var model in models)
        {
            var key = (server, model.Id);

            if (model.IsLoading)
            {
                _loadingSince.TryAdd(key, now);
                continue;
            }

            if (_loadingSince.Remove(key, out var startedAt))
            {
                completed.Add(new ModelLoadEvent
                {
                    Id = Guid.CreateVersion7(),
                    Server = server,
                    Model = model.Id,
                    StartedAt = startedAt,
                    CompletedAt = now,
                    Succeeded = model.IsLoaded && !model.Failed,
                });
            }
        }

        var listed = models.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var vanished in _loadingSince.Keys.Where(k => k.Server == server && !listed.Contains(k.Model)).ToList())
        {
            _loadingSince.Remove(vanished);
        }

        return completed;
    }
}
