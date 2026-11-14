namespace Sentium.Identity.Features.Account;

public sealed record SessionResponse(string? Sub, string? Name, string[] Roles);
