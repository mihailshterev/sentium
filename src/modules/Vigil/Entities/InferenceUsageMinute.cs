using Sentium.Core.Common;

namespace Sentium.Vigil.Entities;

/// <summary>
/// One minute of inference requests as Sentium's own clients saw them, per caller pipeline, model and operation,
/// from the Microsoft.Extensions.AI GenAI metrics. Like <see cref="InferenceServerMinute"/>, readers sum
/// duplicate minutes.
/// </summary>
internal sealed class InferenceUsageMinute : INonAudited
{
    public long Id { get; set; }

    public long EpochMinute { get; set; }

    public string Caller { get; set; } = null!;

    public string Model { get; set; } = null!;

    /// <summary>
    /// The GenAI operation name, such as <c>chat</c> or <c>embeddings</c>.
    /// </summary>
    public string Operation { get; set; } = null!;

    public long Requests { get; set; }

    public long Errors { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public double DurationSeconds { get; set; }

    /// <summary>
    /// Summed time to the first streamed chunk; only streaming requests report one.
    /// </summary>
    public double FirstChunkSeconds { get; set; }

    public long FirstChunkCount { get; set; }
}
