using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;

namespace Sentium.Probatio.Data;

internal sealed class ProbatioDbContextFactory : IDesignTimeDbContextFactory<ProbatioDbContext>
{
    public ProbatioDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProbatioDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, ProbatioDbContext.Schema);

        return new ProbatioDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSovereign => false;
        public bool IsSystem => true;
    }
}
