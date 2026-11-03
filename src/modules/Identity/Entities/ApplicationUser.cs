using Microsoft.AspNetCore.Identity;

namespace Sentium.Identity.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; }
}
