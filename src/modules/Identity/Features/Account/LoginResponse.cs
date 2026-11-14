namespace Sentium.Identity.Features.Account;

public sealed record LoginResponse(string? RedirectUrl = null, bool RequiresTwoFactor = false);
