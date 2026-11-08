using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;

namespace Sentium.Tempus.Data;

public sealed class TempusDbContextFactory : IDesignTimeDbContextFactory<TempusDbContext>
{
    public TempusDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TempusDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, TempusDbContext.Schema);

        return new TempusDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSovereign => false;
        public bool IsSystem => true;
    }
}
