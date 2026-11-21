using Sentium.Core.Common;
using Sentium.Infrastructure.Inference;

namespace Sentium.Vigil.Entities;

/// <summary>
/// A model load the collector watched from <c>loading</c> to its outcome. The times are when the collector first
/// saw each state, so they are accurate to one sampling interval.
/// </summary>
internal sealed class ModelLoadEvent : INonAudited
{
    public Guid Id { get; set; }

    public InferenceEndpoint Server { get; set; }

    public string Model { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public bool Succeeded { get; set; }
}
