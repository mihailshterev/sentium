using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Locus.Data;

internal sealed class LocusDbContextFactory : IDesignTimeDbContextFactory<LocusDbContext>
{
    public LocusDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LocusDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, LocusDbContext.Schema);

        return new LocusDbContext(optionsBuilder.Options);
    }
}
