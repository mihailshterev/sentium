using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Praxis.Features.Speech;
using Sentium.Tests.Integration.Common;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// The monolith with both speech endpoints configured and their HttpClients answered by <see cref="Speech"/>
/// instead of real containers, so the endpoints run through the real OpenAI audio clients.
/// </summary>
public sealed class SpeechApiFactory : MonolithWebApplicationFactory
{
    public const string TranscriptionEndpoint = "http://whisper.test";
    public const string SynthesisEndpoint = "http://tts.test";
    public const int SynthesesPerMinute = 5;

    public StubSpeechServer Speech { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("Speech:TranscriptionEndpoint", TranscriptionEndpoint);
        builder.UseSetting("Speech:SynthesisEndpoint", SynthesisEndpoint);
        builder.UseSetting("Speech:SynthesesPerMinute", SynthesesPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));

        builder.ConfigureServices(services =>
        {
            foreach (var clientName in new[] { SpeechService.TranscriptionHttpClientName, SpeechService.SynthesisHttpClientName })
            {
                services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => Speech);
            }
        });
    }
}
