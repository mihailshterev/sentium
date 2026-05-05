using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Data;

public sealed class AgentRuntimeDbContextFactory : IDesignTimeDbContextFactory<AgentRuntimeDbContext>
{
    public AgentRuntimeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Sentium.AgentRuntime.Api"))
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AgentRuntimeDbContext>();
        var connectionString = configuration.GetConnectionString(ResourceNames.AgentRuntimeDb);

        optionsBuilder.UseSqlServer(connectionString);

        return new AgentRuntimeDbContext(optionsBuilder.Options);
    }
}
