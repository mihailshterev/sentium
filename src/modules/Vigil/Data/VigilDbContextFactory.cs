using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Vigil.Data;

internal sealed class VigilDbContextFactory : IDesignTimeDbContextFactory<VigilDbContext>
{
    public VigilDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VigilDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, VigilDbContext.Schema);

        return new VigilDbContext(optionsBuilder.Options);
    }
}
