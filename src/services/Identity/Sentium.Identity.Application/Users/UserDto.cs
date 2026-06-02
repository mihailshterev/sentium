namespace Sentium.Identity.Application.Users;

public sealed record UserDto(
    Guid Id,
    string? Email,
    string FirstName,
    string? LastName,
    DateTimeOffset? LockoutEnd)
{
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
}
