using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Sentium.Infrastructure.Speech;

namespace Sentium.Praxis.Features.Speech;

// Microsoft.Extensions.AI's speech-to-text and text-to-speech clients are still marked experimental.
#pragma warning disable MEAI001

/// <summary>
/// <see cref="ISpeechService"/> over the OpenAI-compatible audio API of the speech containers (whisper.cpp for
/// recognition, Kokoro for synthesis), through Microsoft.Extensions.AI's speech clients.
/// </summary>
internal sealed class SpeechService(
    IHttpClientFactory httpClientFactory,
    IOptions<SpeechOptions> options,
    IMemoryCache cache,
    ILogger<SpeechService> logger) : ISpeechService
{
    internal const string TranscriptionHttpClientName = "praxis-speech-transcription";
    internal const string SynthesisHttpClientName = "praxis-speech-synthesis";

    // The OpenAI SDK rejects an empty credential; the speech servers run without authentication on the internal network.
    private const string NoApiKey = "unused";
    /// <summary>
    /// The OpenAI SDK wraps a request that never got a response (connection refused, timeout) in a
    /// <see cref="ClientResultException"/> with this status.
    /// </summary>
    private const int NoResponseStatus = 0;
    private const string AudioFormat = "mp3";
    private const string AudioContentType = "audio/mpeg";
    private const string HealthPath = "health";
    private const string VoicesPath = "v1/audio/voices";
    private const string VoicesCacheKey = "speech-voices";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ReadinessCacheDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan VoicesCacheDuration = TimeSpan.FromMinutes(5);

    private SpeechOptions Options => options.Value;

    public async Task<SpeechStatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var transcription = IsReadyAsync(TranscriptionHttpClientName, Options.TranscriptionEndpoint, ct);
        var synthesis = IsReadyAsync(SynthesisHttpClientName, Options.SynthesisEndpoint, ct);
        await Task.WhenAll(transcription, synthesis);

        var voices = await synthesis ? await GetVoicesAsync(ct) : [];

        return new SpeechStatusResponse(
            await transcription,
            voices.Count > 0,
            Options.DefaultVoice,
            voices,
            SpeechLimits.MaxTextLength);
    }

    public async Task<SpeechResult<string>> TranscribeAsync(Stream audio, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(audio);

        if (!SpeechOptions.IsConfigured(Options.TranscriptionEndpoint))
        {
            return SpeechResult<string>.Unavailable;
        }

        try
        {
            using var client = CreateAudioClient(TranscriptionHttpClientName, Options.TranscriptionEndpoint!, Options.TranscriptionModel)
                .AsISpeechToTextClient();

            var response = await client.GetTextAsync(audio, new SpeechToTextOptions { SpeechLanguage = Options.Language }, ct);

            return SpeechResult<string>.Success(response.Text.Trim());
        }
        catch (ClientResultException ex) when (ex.Status != NoResponseStatus)
        {
            logger.LogWarning("Speech-to-text server rejected a recording with status {StatusCode}", ex.Status);
            return SpeechResult<string>.Failed;
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogWarning(ex, "Speech-to-text server unavailable");
            return SpeechResult<string>.Unavailable;
        }
    }

    public async Task<SpeechResult<SpeechAudio>> SynthesizeAsync(string text, string? voice, float? speed, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (!SpeechOptions.IsConfigured(Options.SynthesisEndpoint))
        {
            return SpeechResult<SpeechAudio>.Unavailable;
        }

        try
        {
            using var client = CreateAudioClient(SynthesisHttpClientName, Options.SynthesisEndpoint!, Options.SynthesisModel)
                .AsITextToSpeechClient();

            var speechOptions = new TextToSpeechOptions
            {
                VoiceId = voice ?? Options.DefaultVoice,
                Speed = speed,
                AudioFormat = AudioFormat
            };
            var response = await client.GetAudioAsync(text, speechOptions, ct);

            var audio = response.Contents.OfType<DataContent>().FirstOrDefault();
            if (audio is null || audio.Data.IsEmpty)
            {
                logger.LogWarning("Text-to-speech server returned no audio");
                return SpeechResult<SpeechAudio>.Failed;
            }

            return SpeechResult<SpeechAudio>.Success(new SpeechAudio(audio.Data, AudioContentType));
        }
        catch (ClientResultException ex) when (ex.Status != NoResponseStatus)
        {
            logger.LogWarning("Text-to-speech server rejected a request with status {StatusCode}", ex.Status);
            return SpeechResult<SpeechAudio>.Failed;
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogWarning(ex, "Text-to-speech server unavailable");
            return SpeechResult<SpeechAudio>.Unavailable;
        }
    }

    /// <summary>
    /// Reads <c>{"voices":[...]}</c> where each entry is either a voice id or an object with <c>id</c> and
    /// optionally <c>name</c>; servers differ on which.
    /// </summary>
    internal static IReadOnlyList<SpeechVoice> ParseVoices(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("voices", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var voices = new List<SpeechVoice>();
        foreach (var entry in list.EnumerateArray())
        {
            var id = entry.ValueKind == JsonValueKind.Object ? ReadString(entry, "id") : ReadString(entry);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var name = entry.ValueKind == JsonValueKind.Object ? ReadString(entry, "name") : null;
            voices.Add(new SpeechVoice(id, string.IsNullOrWhiteSpace(name) ? id : name));
        }

        return voices.OrderBy(v => v.Id, StringComparer.Ordinal).ToList();
    }

    private static string? ReadString(JsonElement element, string? property = null)
    {
        if (property is not null && !element.TryGetProperty(property, out element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static bool IsUnavailable(Exception ex, CancellationToken ct)
    {
        return ex switch
        {
            HttpRequestException or TimeoutRejectedException or BrokenCircuitException => true,
            ClientResultException { Status: NoResponseStatus } => true,
            OperationCanceledException => !ct.IsCancellationRequested,
            _ => false
        };
    }

    private async Task<bool> IsReadyAsync(string httpClientName, Uri? endpoint, CancellationToken ct)
    {
        if (!SpeechOptions.IsConfigured(endpoint))
        {
            return false;
        }

        return await cache.GetOrCreateAsync($"speech-ready:{httpClientName}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReadinessCacheDuration;
            return ProbeAsync(httpClientName, endpoint!, ct);
        });
    }

    private async Task<bool> ProbeAsync(string httpClientName, Uri endpoint, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ProbeTimeout);

            using var response = await httpClientFactory.CreateClient(httpClientName).GetAsync(new Uri(endpoint, HealthPath), timeout.Token);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<SpeechVoice>> GetVoicesAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(VoicesCacheKey, out IReadOnlyList<SpeechVoice>? cached) && cached is not null)
        {
            return cached;
        }

        var voices = await LoadVoicesAsync(ct);
        if (voices.Count > 0)
        {
            cache.Set(VoicesCacheKey, voices, VoicesCacheDuration);
        }

        return voices;
    }

    private async Task<IReadOnlyList<SpeechVoice>> LoadVoicesAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ProbeTimeout);

            var client = httpClientFactory.CreateClient(SynthesisHttpClientName);
            using var response = await client.GetAsync(new Uri(Options.SynthesisEndpoint!, VoicesPath), timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);

            return ParseVoices(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException || IsUnavailable(ex, ct))
        {
            logger.LogWarning(ex, "Could not list the text-to-speech voices");
            return [];
        }
    }

    private AudioClient CreateAudioClient(string httpClientName, Uri endpoint, string model)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri($"{endpoint.AbsoluteUri.TrimEnd('/')}/v1"),
            Transport = new HttpClientPipelineTransport(httpClientFactory.CreateClient(httpClientName)),
            // The named HttpClient's resilience handler owns timeouts, and nothing here is retried.
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
            NetworkTimeout = Options.Timeout
        };

        return new AudioClient(model, new ApiKeyCredential(NoApiKey), clientOptions);
    }
}
