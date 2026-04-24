using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AgentRuntime.Infrastructure.Data;

public sealed class AgentRuntimeDbContextFactory : IDesignTimeDbContextFactory<AgentRuntimeDbContext>
{
    public AgentRuntimeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "AgentRuntime.Api"))
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AgentRuntimeDbContext>();
        var connectionString = configuration.GetConnectionString("agentruntimedb");

        optionsBuilder.UseSqlServer(connectionString);

        return new AgentRuntimeDbContext(optionsBuilder.Options);
    }
}
