namespace Sentium.Identity.Contracts;

/// <param name="LastSeenAt">
/// When the Device last called the server, persisted at most every few minutes; <see langword="null"/> until its
/// first call after pairing.
/// </param>
public sealed record DeviceInfo(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string? OwnerUserName,
    string? AppVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt);
