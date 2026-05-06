namespace Sentium.Identity.Api.Contracts.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string? LastName,
    IList<string> Roles,
    bool IsLockedOut);
