namespace Sentium.Infrastructure.Realtime;

/// <param name="Topic">One of <see cref="RealtimeTopics"/>.</param>
/// <param name="Data">
/// Optional payload, serialized as JSON. Most events carry none: they only tell the portal to refetch.
/// </param>
public sealed record RealtimeEvent(string Topic, RealtimeAudience Audience, object? Data = null);
