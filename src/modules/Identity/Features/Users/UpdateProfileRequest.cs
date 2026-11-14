using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Features.Users;

internal sealed record UpdateProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [MaxLength(100)] string? LastName,
    [Required, MinLength(3), MaxLength(64)] string UserName);
