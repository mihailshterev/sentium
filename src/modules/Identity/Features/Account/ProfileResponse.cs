namespace Sentium.Identity.Features.Account;

public sealed record ProfileResponse(Guid Id, string? UserName, string FirstName, string? LastName);
