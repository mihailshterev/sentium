using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Sentium.Shared.Constants;

namespace Sentium.Sandbox.Infrastructure.Data;

public sealed class SandboxDbContextFactory : IDesignTimeDbContextFactory<SandboxDbContext>
{
    public SandboxDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Sentium.Sandbox.Api"))
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(ResourceNames.SandboxDb);

        var optionsBuilder = new DbContextOptionsBuilder<SandboxDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new SandboxDbContext(optionsBuilder.Options);
    }
}
