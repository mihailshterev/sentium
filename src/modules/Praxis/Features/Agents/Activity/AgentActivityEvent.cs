using Sentium.Praxis.Common.Streaming;

namespace Sentium.Praxis.Features.Agents.Activity;

/// <summary>
/// A streamed activity event: a frame type (see <see cref="AgentUpdateTypes"/>) and its text payload.
/// </summary>
public readonly record struct AgentActivityEvent(string Type, string Payload);
