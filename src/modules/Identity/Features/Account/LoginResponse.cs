namespace Sentium.Identity.Features.Account;

internal sealed record LoginResponse(string? RedirectUrl = null, bool RequiresTwoFactor = false);
