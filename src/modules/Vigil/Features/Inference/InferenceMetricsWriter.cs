using Sentium.Vigil.Data;
using Sentium.Vigil.Entities;

namespace Sentium.Vigil.Features.Inference;

internal sealed class InferenceMetricsWriter(VigilDbContext db)
{
    public async Task WriteAsync(
        IReadOnlyList<InferenceServerMinute> serverMinutes,
        IReadOnlyList<InferenceUsageMinute> usageMinutes,
        IReadOnlyList<ModelLoadEvent> loadEvents,
        CancellationToken ct)
    {
        if (serverMinutes.Count == 0 && usageMinutes.Count == 0 && loadEvents.Count == 0)
        {
            return;
        }

        db.InferenceServerMinutes.AddRange(serverMinutes);
        db.InferenceUsageMinutes.AddRange(usageMinutes);
        db.ModelLoadEvents.AddRange(loadEvents);

        await db.SaveChangesAsync(ct);
    }
}
