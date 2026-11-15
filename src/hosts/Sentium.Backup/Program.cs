using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentium.Backup.Options;
using Sentium.Backup.Parts;
using Sentium.Backup.Restore;
using Sentium.Backup.Running;
using Sentium.Backup.Status;
using Sentium.Backup.Tools;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Diagnostics;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Mvc;

namespace Sentium.Backup;

/// <summary>
/// The backup sidecar. Without arguments it serves the status and trigger API and runs the nightly schedule;
/// with <c>restore</c> or <c>verify</c> it works on one archive and exits.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (RestoreArguments.IsCommand(args))
        {
            return await RunCommandAsync(args);
        }

        await RunServiceAsync(args);
        return 0;
    }

    private static async Task RunServiceAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();
        builder.AddInternalApiSecurity();

        builder.Services.AddSentiumProblemDetails();
        builder.Services.AddControllers().AddInternalControllers();

        AddBackupServices(builder);
        builder.Services.AddSingleton<BackupCatalog>();
        builder.Services.AddSingleton<BackupRunner>();
        builder.Services.AddSingleton<BackupCoordinator>();
        builder.Services.AddHostedService<BackupScheduler>();

        var app = builder.Build();

        app.UseExceptionHandler();
        app.MapDefaultEndpoints();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }

    private static async Task<int> RunCommandAsync(string[] args)
    {
        var arguments = RestoreArguments.Parse(args);
        if (arguments is null)
        {
            await Console.Error.WriteLineAsync(RestoreArguments.Usage);
            return 2;
        }

        // The verbs are not configuration; keep them away from the command-line configuration provider.
        var builder = Host.CreateApplicationBuilder([]);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        AddBackupServices(builder);
        builder.Services.AddSingleton<RestoreCommand>();

        using var host = builder.Build();
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        try
        {
            return await host.Services.GetRequiredService<RestoreCommand>().RunAsync(arguments, lifetime.ApplicationStopping);
        }
        catch (BackupFailedException ex)
        {
            await Console.Error.WriteLineAsync($"Restore failed: {ex.Message}");
            return 1;
        }
    }

    private static void AddBackupServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<BackupOptions>()
            .Bind(builder.Configuration.GetSection(BackupOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);

        PostgresEnvironment.ApplyConnectionString(builder.Configuration);

        var blobConnection = builder.Configuration.GetConnectionString(ResourceNames.WorkspaceBlobs)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ResourceNames.WorkspaceBlobs} is not set.");
        builder.Services.AddSingleton(new BlobServiceClient(blobConnection));
        builder.Services.AddSingleton<BlobPart>();
        builder.Services.AddSingleton<KeysPart>();
    }
}
