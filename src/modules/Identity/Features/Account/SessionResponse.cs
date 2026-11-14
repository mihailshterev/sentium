namespace Sentium.Identity.Features.Account;

internal sealed record SessionResponse(string? Sub, string? Name, string[] Roles);
