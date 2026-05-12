using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Sentium.Infrastructure.Messaging;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Infrastructure.Policies;
using Sentium.Shared.Constants;

namespace Sentium.Sentinel.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.AddSingleton<IEventBus, NatsEventBus>();

        services.AddChatClient(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var pdpOpts = sp.GetRequiredService<IOptions<PdpOptions>>().Value;

            var ollamaConnectionString =
                configuration.GetConnectionString(ResourceNames.Ollama)
                ?? configuration["AI:OllamaBaseUrl"]
                ?? "http://localhost:11434";

            var ollamaBaseUrl = ParseEndpointFromConnectionString(ollamaConnectionString);

            return new OllamaApiClient(new Uri(ollamaBaseUrl))
            {
                SelectedModel = pdpOpts.IntentCheckModel
            };
        });

        services.AddSingleton<IPdpPolicy, SemanticIntentPolicy>();

        return builder;
    }

    private static string ParseEndpointFromConnectionString(string value)
    {
        foreach (var segment in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx > 0 && segment[..idx].Trim().Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                return segment[(idx + 1)..].Trim();
            }
        }

        return value;
    }
}

