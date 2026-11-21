using Sentium.Core.Common;
using Sentium.Infrastructure.Inference;

namespace Sentium.Vigil.Entities;

/// <summary>
/// One minute of one model's work as llama-server counts it: the token and time deltas of its <c>/metrics</c>
/// counters, and the busiest queue seen. A restart within a minute can leave two rows for the same minute and
/// model; readers sum them.
/// </summary>
internal sealed class InferenceServerMinute : INonAudited
{
    public long Id { get; set; }

    /// <summary>
    /// Minutes since the Unix epoch (UTC) at the start of the bucket.
    /// </summary>
    public long EpochMinute { get; set; }

    public InferenceEndpoint Server { get; set; }

    public string Model { get; set; } = null!;

    public long PromptTokens { get; set; }

    public double PromptSeconds { get; set; }

    public long GeneratedTokens { get; set; }

    public double GenerationSeconds { get; set; }

    public int PeakRequestsProcessing { get; set; }

    public int PeakRequestsDeferred { get; set; }

    public int Samples { get; set; }

    /// <summary>
    /// Samples in which at least one request was processing.
    /// </summary>
    public int BusySamples { get; set; }
}
