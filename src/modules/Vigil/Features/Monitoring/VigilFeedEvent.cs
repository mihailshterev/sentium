namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// One live update for the portal: <paramref name="Type"/> is a <see cref="VigilFeedEventTypes"/> value,
/// <paramref name="Data"/> a <c>ServiceHealthPayload</c> or <see cref="Incident"/>.
/// </summary>
public sealed record VigilFeedEvent(string Type, object Data);
