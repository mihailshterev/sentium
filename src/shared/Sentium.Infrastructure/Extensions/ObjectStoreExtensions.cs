using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentium.Infrastructure.Storage;

namespace Sentium.Infrastructure.Extensions;

public static class ObjectStoreExtensions
{
    private const string HealthCheckName = "object-storage";

    /// <summary>
    /// Registers <see cref="IObjectStore"/> on the S3-compatible server named by
    /// <c>ConnectionStrings:{connectionName}</c> (see <see cref="ObjectStoreSettings"/>), with a health check. The
    /// connection string is read on first use, so test hosts can still override it after registration.
    /// </summary>
    public static IHostApplicationBuilder AddSentiumObjectStore(this IHostApplicationBuilder builder, string connectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        builder.Services.AddSingleton<IObjectStore>(sp =>
            new S3ObjectStore(ObjectStoreSettings.Parse(sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionName))));

        builder.Services.AddHealthChecks().AddCheck<ObjectStoreHealthCheck>(HealthCheckName);

        return builder;
    }
}
