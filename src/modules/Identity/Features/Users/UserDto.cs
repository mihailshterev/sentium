namespace Sentium.Identity.Features.Users;

internal sealed record UserDto(
    Guid Id,
    string? UserName,
    string FirstName,
    string? LastName,
    DateTimeOffset? LockoutEnd,
    IReadOnlyList<string> Roles,
    DateTimeOffset? AvatarUpdatedAt)
{
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
}
