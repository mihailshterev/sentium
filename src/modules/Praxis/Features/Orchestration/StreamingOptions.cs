using System.ComponentModel.DataAnnotations;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// The <c>Orchestration</c> configuration section: how workflow runs execute and how their streams (and the assistant's)
/// are buffered and served.
/// </summary>
internal sealed class StreamingOptions
{
    public const string SectionName = "Orchestration";

    /// <summary>
    /// How long a stream nobody reads stays in the relay after its last update.
    /// </summary>
    public TimeSpan StreamIdleTtl { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// The longest a single SSE connection stays open; the client then reconnects and resumes from its last id.
    /// </summary>
    public TimeSpan StreamMaxDuration { get; set; } = TimeSpan.FromMinutes(30);

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long consecutive text tokens are gathered into one frame. Each frame is a relay entry, an SSE event and a
    /// client update, so joining tokens keeps a long answer to a few frames a second.
    /// </summary>
    public TimeSpan TextCoalesceWindow { get; set; } = TimeSpan.FromMilliseconds(50);

    [Range(1, 64)]
    public int MaxConcurrentWorkflows { get; set; } = 4;

    public TimeSpan WorkflowTimeout { get; set; } = TimeSpan.FromMinutes(120);
}
