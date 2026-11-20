using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Praxis;

/// <summary>
/// The speech endpoints against a stub speech server: the happy paths, the limits, and the unavailable case.
/// Each test acts as its own user, because the request budgets are counted per user.
/// </summary>
public sealed class SpeechApiTests : IClassFixture<SpeechApiFactory>, IDisposable
{
    private const string StatusUrl = "/api/praxis/speech";
    private const string TranscribeUrl = "/api/praxis/speech/transcribe";
    private const string SpeakUrl = "/api/praxis/speech/speak";

    private readonly SpeechApiFactory _factory;
    private readonly HttpClient _client;

    public SpeechApiTests(SpeechApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
        _factory.Speech.Reset();
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, Guid.NewGuid().ToString());
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Transcribe_SendsTheRecordingToTheSpeechServer_AndReturnsTheTrimmedTranscript()
    {
        using var form = Recording([1, 2, 3, 4]);

        var response = await _client.PostAsync(TranscribeUrl, form, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("The drill is in the garage on the second shelf.", body.GetProperty("text").GetString());

        var upstream = _factory.Speech.Requests.Single(r => r.Uri.AbsolutePath == "/v1/audio/transcriptions");
        Assert.Equal("whisper.test", upstream.Uri.Host);
        Assert.Contains("name=file", upstream.Body, StringComparison.Ordinal);
        Assert.Contains("whisper-1", upstream.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transcribe_Returns400_WithoutARecording()
    {
        using var note = new StringContent("x");
        using var form = new MultipartFormDataContent { { note, "note" } };

        var response = await _client.PostAsync(TranscribeUrl, form, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Speech.Requests);
    }

    [Fact]
    public async Task Transcribe_Returns422_WhenTheSpeechServerCannotReadTheRecording()
    {
        _factory.Speech.RejectRecordings = true;
        using var form = Recording([9, 9, 9]);

        var response = await _client.PostAsync(TranscribeUrl, form, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Speak_ReturnsTheSynthesisedAudio_WithTheChosenVoiceAndSpeed()
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text = "The drill is in the garage.", voice = "am_adam", speed = 1.5 }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(StubSpeechServer.Audio, await response.Content.ReadAsByteArrayAsync(Ct));

        var upstream = _factory.Speech.Requests.Single(r => r.Uri.AbsolutePath == "/v1/audio/speech");
        Assert.Equal("tts.test", upstream.Uri.Host);
        using var sent = JsonDocument.Parse(upstream.Body);
        Assert.Equal("The drill is in the garage.", sent.RootElement.GetProperty("input").GetString());
        Assert.Equal("am_adam", sent.RootElement.GetProperty("voice").GetString());
        Assert.Equal("kokoro", sent.RootElement.GetProperty("model").GetString());
        Assert.Equal("mp3", sent.RootElement.GetProperty("response_format").GetString());
        Assert.Equal(1.5, sent.RootElement.GetProperty("speed").GetDouble());
    }

    [Fact]
    public async Task Speak_UsesTheDefaultVoice_WhenNoneIsChosen()
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text = "Hello there." }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var sent = JsonDocument.Parse(_factory.Speech.Requests.Single(r => r.Uri.AbsolutePath == "/v1/audio/speech").Body);
        Assert.Equal("af_heart", sent.RootElement.GetProperty("voice").GetString());
    }

    [Theory]
    [InlineData("", null, null)]
    [InlineData("Hello.", "not a voice id", null)]
    [InlineData("Hello.", null, 3.0)]
    public async Task Speak_Returns400_ForAnInvalidRequest(string text, string? voice, double? speed)
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text, voice, speed }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.Speech.Requests);
    }

    [Fact]
    public async Task Speak_Returns400_WhenTheTextIsOverTheLimit()
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text = new string('a', 1001) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Speak_Returns429_OnceTheUsersBudgetForTheMinuteIsSpent()
    {
        for (var i = 0; i < SpeechApiFactory.SynthesesPerMinute; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync(SpeakUrl, new { text = $"Sentence {i}." }, Ct)).StatusCode);
        }

        var overBudget = await _client.PostAsJsonAsync(SpeakUrl, new { text = "One too many." }, Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, overBudget.StatusCode);
        Assert.Equal(SpeechApiFactory.SynthesesPerMinute, _factory.Speech.Requests.Count(r => r.Uri.AbsolutePath == "/v1/audio/speech"));
    }

    [Fact]
    public async Task Endpoints_Return503_WhenTheSpeechServerIsDown()
    {
        _factory.Speech.IsUp = false;
        using var form = Recording([1, 2, 3]);

        var transcribe = await _client.PostAsync(TranscribeUrl, form, Ct);
        var speak = await _client.PostAsJsonAsync(SpeakUrl, new { text = "Hello there." }, Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, transcribe.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, speak.StatusCode);
    }

    [Fact]
    public async Task Status_ListsTheVoices_WhenBothServersAreUp()
    {
        var status = await _client.GetFromJsonAsync<JsonElement>(StatusUrl, Ct);

        Assert.True(status.GetProperty("transcription").GetBoolean());
        Assert.True(status.GetProperty("synthesis").GetBoolean());
        Assert.Equal("af_heart", status.GetProperty("defaultVoice").GetString());
        Assert.Equal(new string[] { "af_heart", "am_adam" }, status.GetProperty("voices").EnumerateArray().Select(v => v.GetProperty("id").GetString()));
    }

    [Fact]
    public async Task WithoutSpeechConfigured_StatusReportsUnavailable_AndTheEndpointsReturn503()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("Speech:TranscriptionEndpoint", string.Empty)
            .UseSetting("Speech:SynthesisEndpoint", string.Empty));
        using var client = factory.CreateClient();
        using var form = Recording([1, 2, 3]);

        var status = await client.GetFromJsonAsync<JsonElement>(StatusUrl, Ct);
        var transcribe = await client.PostAsync(TranscribeUrl, form, Ct);
        var speak = await client.PostAsJsonAsync(SpeakUrl, new { text = "Hello there." }, Ct);

        Assert.False(status.GetProperty("transcription").GetBoolean());
        Assert.False(status.GetProperty("synthesis").GetBoolean());
        Assert.Equal(0, status.GetProperty("voices").GetArrayLength());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, transcribe.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, speak.StatusCode);
        Assert.Empty(_factory.Speech.Requests);
    }

    /// <summary>
    /// The form owns the file part and disposes it with itself.
    /// </summary>
    private static MultipartFormDataContent Recording(byte[] audio)
    {
        var form = new MultipartFormDataContent();
#pragma warning disable CA2000
        var file = new ByteArrayContent(audio);
#pragma warning restore CA2000
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        form.Add(file, "file", "speech.webm");

        return form;
    }
}
