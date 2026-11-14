namespace Sentium.Identity.Features.Users;

public sealed record UserResponse(
    Guid Id,
    string UserName,
    string FirstName,
    string? LastName,
    IList<string> Roles,
    bool IsLockedOut);
