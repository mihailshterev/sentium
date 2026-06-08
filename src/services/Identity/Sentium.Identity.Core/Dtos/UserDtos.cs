using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Core.Dtos;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string? LastName,
    IList<string> Roles,
    bool IsLockedOut);

public sealed record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [MaxLength(100)] string? LastName,
    [Required, EmailAddress, MaxLength(256)] string Email);
