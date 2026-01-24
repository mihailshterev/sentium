using Sentinel.Core.Policies;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Infrastructure.Policies;
using Sentinel.Infrastructure.Sensors;
using System.Runtime.InteropServices;
using Sentinel.Core.Sensors;
using Sentinel.Infrastructure.Workers;

namespace Sentinel.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISentinelPolicy, BlockOutboundNetworkPolicy>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<INetworkSensor, WindowsNetworkSensor>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<INetworkSensor, LinuxNetworkSensor>();
        }
        services.AddHostedService<NetworkWorker>();
        return services;
    }
}
