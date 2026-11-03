using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Registry.Data;

public sealed class RegistryDbContextFactory : IDesignTimeDbContextFactory<RegistryDbContext>
{
    public RegistryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RegistryDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, RegistryDbContext.Schema);

        return new RegistryDbContext(optionsBuilder.Options);
    }
}
