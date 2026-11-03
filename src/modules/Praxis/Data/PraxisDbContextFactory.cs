using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;

namespace Sentium.Praxis.Data;

public sealed class PraxisDbContextFactory : IDesignTimeDbContextFactory<PraxisDbContext>
{
    public PraxisDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PraxisDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, PraxisDbContext.Schema);

        return new PraxisDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSovereign => false;
        public bool IsSystem => true;
    }
}
