using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Sentium.Infrastructure.Extensions;
using Sentium.Infrastructure.Messaging;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Audit;
using Sentium.Sentinel.Infrastructure.Audit;
using Sentium.Sentinel.Infrastructure.Data;
using Sentium.Shared.Constants;

namespace Sentium.Sentinel.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        builder.AddAuditedDbContext<SentinelDbContext>(ResourceNames.SentinelDb);

        services.AddScoped<IAuditLog, EfCoreAuditLog>();

        services.AddSingleton<IEventBus, NatsEventBus>();

        services.AddChatClient(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var pdpOpts = sp.GetRequiredService<IOptions<PdpOptions>>().Value;

            var ollamaUri = new Uri(configuration["AI:OllamaBaseUrl"] ?? "http://localhost:11434");

            return new OllamaApiClient(ollamaUri)
            {
                SelectedModel = pdpOpts.IntentCheckModel
            };
        });

        return builder;
    }
}

