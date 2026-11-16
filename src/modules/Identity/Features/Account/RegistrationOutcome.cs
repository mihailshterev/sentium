using Microsoft.AspNetCore.Identity;
using Sentium.Identity.Entities;

namespace Sentium.Identity.Features.Account;

/// <param name="Closed">Registration is closed, so nothing was created.</param>
internal sealed record RegistrationOutcome(IdentityResult Result, ApplicationUser? User, bool Closed = false)
{
    public static RegistrationOutcome ClosedToNewAccounts { get; } = new(IdentityResult.Failed(), null, Closed: true);
}
