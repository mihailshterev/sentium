using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;

namespace Sentium.Memoria.Data;

internal sealed class MemoriaDbContextFactory : IDesignTimeDbContextFactory<MemoriaDbContext>
{
    public MemoriaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MemoriaDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, MemoriaDbContext.Schema);

        return new MemoriaDbContext(optionsBuilder.Options, new DesignTimeCurrentUser());
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public Guid? UserId => null;
        public bool IsAuthenticated => false;
        public bool IsSovereign => false;
        public bool IsSystem => true;
    }
}
