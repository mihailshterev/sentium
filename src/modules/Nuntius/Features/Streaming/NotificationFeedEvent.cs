namespace Sentium.Nuntius.Features.Streaming;

/// <param name="Type">One of <see cref="NotificationFeedEventTypes"/>.</param>
internal sealed record NotificationFeedEvent(string Type, object? Data = null);
