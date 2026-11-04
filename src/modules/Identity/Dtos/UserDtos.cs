using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Dtos;

public sealed record UserResponse(
    Guid Id,
    string UserName,
    string FirstName,
    string? LastName,
    IList<string> Roles,
    bool IsLockedOut);

public sealed record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [MaxLength(100)] string? LastName,
    [Required, MinLength(3), MaxLength(64)] string UserName);
