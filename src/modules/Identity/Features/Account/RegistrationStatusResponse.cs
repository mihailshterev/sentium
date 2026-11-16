namespace Sentium.Identity.Features.Account;

/// <param name="Bootstrap">No account exists yet: the next one to register becomes the Sovereign.</param>
internal sealed record RegistrationStatusResponse(bool Open, bool Bootstrap);
