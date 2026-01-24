using Microsoft.AspNetCore.Identity;

namespace IdentityProvider.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = null!;
    public string? LastName { get; set; } = null!;
}
