using Microsoft.AspNetCore.Identity;

namespace Sentium.Identity.Entities;

internal sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
}
