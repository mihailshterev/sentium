using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Security;
using Sentium.Sandbox.Application.Artifacts;
using Sentium.Sandbox.Application.Options;
using Sentium.Sandbox.Application.Sentinel;
using Sentium.Sandbox.Core.Interfaces;
using Sentium.Sandbox.Infrastructure.Artifacts;
using Sentium.Sandbox.Infrastructure.Data;
using Sentium.Sandbox.Infrastructure.Docker;
using Sentium.Sandbox.Infrastructure.Logging;
using Sentium.Sandbox.Infrastructure.Sentinel;
using Sentium.Shared.Constants;

namespace Sentium.Sandbox.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddSandboxInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddInternalApiSecurity();

        var services = builder.Services;

        builder.AddAuditedDbContext<SandboxDbContext>(ResourceNames.SandboxDb);

        services.AddScoped<IExecutionLogRepository, EfCoreExecutionLogRepository>();

        builder.AddAzureBlobServiceClient(ResourceNames.WorkspaceBlobs);

        services.AddSingleton<IArtifactService, ArtifactService>();

        services.AddSingleton<IDockerClient>(sp =>
        {
            var sandboxOptions = sp.GetRequiredService<IOptions<SandboxOptions>>().Value;

            var explicitHost = sandboxOptions.DockerHost;
            if (string.IsNullOrWhiteSpace(explicitHost))
            {
                explicitHost = Environment.GetEnvironmentVariable("DOCKER_HOST") ?? string.Empty;
            }

            var config = string.IsNullOrWhiteSpace(explicitHost) ? new DockerClientConfiguration() : new DockerClientConfiguration(new Uri(explicitHost));

            return config.CreateClient();
        });

        services.AddSingleton<ContainerConfigBuilder>();
        services.AddSingleton<IJobDirectoryService, JobDirectoryService>();
        services.AddSingleton<ISandboxRunner, DockerSandboxRunner>();

        services.AddHttpContextAccessor();

        services.AddTransient<InternalApiKeyDelegatingHandler>();

        services.AddHttpClient<ISentinelGateway, HttpSentinelGateway>(client =>
        {
            client.BaseAddress = new Uri($"https+http://{ServiceNames.Sentinel}");
        }).AddHttpMessageHandler<InternalApiKeyDelegatingHandler>().AddStandardResilienceHandler();

        return builder;
    }
}
