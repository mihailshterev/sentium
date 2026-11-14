namespace Sentium.Identity.Features.Account;

internal sealed record ProfileResponse(Guid Id, string? UserName, string FirstName, string? LastName);
