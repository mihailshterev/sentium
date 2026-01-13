using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityProvider.Infrastructure.Data;

public class IdentityProviderDbContext : IdentityDbContext
{
    public IdentityProviderDbContext(DbContextOptions<IdentityProviderDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }
}
