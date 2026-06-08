namespace Sentium.Identity.Core.Dtos;

public sealed record RegisterResponse(Guid Id, string? Email);

public sealed record LoginResponse(string? RedirectUrl = null, bool RequiresTwoFactor = false);

public sealed record ProfileResponse(Guid Id, string? Email, string FirstName, string? LastName);
