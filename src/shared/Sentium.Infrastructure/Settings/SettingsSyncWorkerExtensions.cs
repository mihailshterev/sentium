using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sentium.Infrastructure.Settings;

public static class SettingsSyncWorkerExtensions
{
    public static IHostApplicationBuilder AddSettingsSyncWorker(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHostedService<SettingsSyncWorker>();
        return builder;
    }
}
