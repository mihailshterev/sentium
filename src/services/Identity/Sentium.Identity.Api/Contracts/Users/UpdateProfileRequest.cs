using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Api.Contracts.Users;

public sealed record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [MaxLength(100)] string? LastName,
    [Required, EmailAddress, MaxLength(256)] string Email);
