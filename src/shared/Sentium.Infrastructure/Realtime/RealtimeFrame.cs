namespace Sentium.Infrastructure.Realtime;

internal sealed record RealtimeFrame(string Topic, object? Data = null);
