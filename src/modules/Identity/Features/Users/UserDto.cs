namespace Sentium.Identity.Features.Users;

public sealed record UserDto(
    Guid Id,
    string? UserName,
    string FirstName,
    string? LastName,
    DateTimeOffset? LockoutEnd)
{
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
}
