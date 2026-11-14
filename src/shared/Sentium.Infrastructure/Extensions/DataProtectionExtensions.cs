using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Sentium.Infrastructure.Extensions;

public static class DataProtectionExtensions
{
    public const string KeysPathKey = "DataProtection:KeysPath";

    private const string ApplicationName = "Sentium";

    public static IHostApplicationBuilder AddSentiumDataProtection(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var dataProtection = builder.Services.AddDataProtection().SetApplicationName(ApplicationName);

        var keysPath = builder.Configuration[KeysPathKey];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        return builder;
    }
}
