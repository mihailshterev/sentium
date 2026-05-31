using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Sentium.Shared.Constants;

namespace Sentium.Registry.Infrastructure.Data;

public sealed class RegistryDbContextFactory : IDesignTimeDbContextFactory<RegistryDbContext>
{
    public RegistryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Sentium.Registry.Api"))
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<RegistryDbContext>();
        var connectionString = configuration.GetConnectionString(ResourceNames.RegistryDb);

        optionsBuilder.UseSqlServer(connectionString);

        return new RegistryDbContext(optionsBuilder.Options);
    }
}
