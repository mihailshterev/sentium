using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Sentium.Shared.Constants;

namespace Sentium.Locus.Infrastructure.Data;

public sealed class LocusDbContextFactory : IDesignTimeDbContextFactory<LocusDbContext>
{
    public LocusDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Sentium.Locus.Api"))
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<LocusDbContext>();
        var connectionString = configuration.GetConnectionString(ResourceNames.LocusDb);

        optionsBuilder.UseSqlServer(connectionString);

        return new LocusDbContext(optionsBuilder.Options);
    }
}
