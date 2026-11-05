using System.ComponentModel.DataAnnotations;

namespace Sentium.Identity.Dtos;

public sealed record RegisterRequest(
    [Required, MinLength(3), MaxLength(64)] string UserName,
    [Required] string Password);

public sealed record LoginRequest(
    [Required] string UserName,
    [Required] string Password);

public sealed record RegisterResponse(Guid Id, string? UserName);

public sealed record LoginResponse(string? RedirectUrl = null, bool RequiresTwoFactor = false);

public sealed record SessionResponse(string? Sub, string? Name, string[] Roles);

public sealed record ProfileResponse(Guid Id, string? UserName, string FirstName, string? LastName);
