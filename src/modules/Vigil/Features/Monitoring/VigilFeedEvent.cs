namespace Sentium.Vigil.Features.Monitoring;

/// <summary>
/// One live update for the portal: <paramref name="Type"/> is a <see cref="VigilFeedEventTypes"/> value,
/// <paramref name="Data"/> a <c>ServiceHealthPayload</c>, an <see cref="Incident"/> or a Device frame.
/// </summary>
/// <param name="AudienceUserId">
/// When set, only this user and Sovereigns receive the update (a Device is visible to its owner and to Sovereigns);
/// <see langword="null"/> broadcasts to every signed-in user.
/// </param>
internal sealed record VigilFeedEvent(string Type, object Data, Guid? AudienceUserId = null);
