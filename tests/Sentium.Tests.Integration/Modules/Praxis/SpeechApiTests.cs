using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("text").GetString().Should().Be("The drill is in the garage on the second shelf.");

        var upstream = _factory.Speech.Requests.Single(r => r.Uri.AbsolutePath == "/v1/audio/transcriptions");
        upstream.Uri.Host.Should().Be("whisper.test");
        upstream.Body.Should().Contain("name=file").And.Contain("whisper-1");
    }

    [Fact]
    public async Task Transcribe_Returns400_WithoutARecording()
    {
        using var note = new StringContent("x");
        using var form = new MultipartFormDataContent { { note, "note" } };

        var response = await _client.PostAsync(TranscribeUrl, form, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Speech.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Transcribe_Returns422_WhenTheSpeechServerCannotReadTheRecording()
    {
        _factory.Speech.RejectRecordings = true;
        using var form = Recording([9, 9, 9]);

        var response = await _client.PostAsync(TranscribeUrl, form, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Speak_ReturnsTheSynthesisedAudio_WithTheChosenVoiceAndSpeed()
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text = "The drill is in the garage.", voice = "am_adam", speed = 1.5 }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadAsByteArrayAsync(Ct)).Should().Equal(StubSpeechServer.Audio);

        var upstream = _factory.Speech.Requests.Single(r => r.Uri.AbsolutePath == "/v1/audio/speech");
        upstream.Uri.Host.Should().Be("tts.test");
        using var sent = JsonDocument.Parse(upstream.Body);
        sent.RootElement.GetProperty("input").GetString().Should().Be("The drill is in the garage.");
        sent.RootElement.GetProperty("voice").GetString().Should().Be("am_adam");
        sent.RootElement.GetProperty("model").GetString().Should().Be("kokoro");
        sent.RootElement.GetProperty("response_format").GetString().Should().Be("mp3");
        sent.RootElement.GetProperty("speed").GetDouble().Should().Be(1.5);
    }

    [Fact]
    public async Task Speak_UsesTheDefaultVoice_WhenNoneIsChosen()
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text = "Hello there." }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var sent = JsonDocument.Parse(_factory.Speech.Requests.Single(r => r.Uri.AbsolutePath == "/v1/audio/speech").Body);
        sent.RootElement.GetProperty("voice").GetString().Should().Be("af_heart");
    }

    [Theory]
    [InlineData("", null, null)]
    [InlineData("Hello.", "not a voice id", null)]
    [InlineData("Hello.", null, 3.0)]
    public async Task Speak_Returns400_ForAnInvalidRequest(string text, string? voice, double? speed)
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text, voice, speed }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Speech.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Speak_Returns400_WhenTheTextIsOverTheLimit()
    {
        var response = await _client.PostAsJsonAsync(SpeakUrl, new { text = new string('a', 1001) }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Speak_Returns429_OnceTheUsersBudgetForTheMinuteIsSpent()
    {
        for (var i = 0; i < SpeechApiFactory.SynthesesPerMinute; i++)
        {
            (await _client.PostAsJsonAsync(SpeakUrl, new { text = $"Sentence {i}." }, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var overBudget = await _client.PostAsJsonAsync(SpeakUrl, new { text = "One too many." }, Ct);

        overBudget.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        _factory.Speech.Requests.Count(r => r.Uri.AbsolutePath == "/v1/audio/speech").Should().Be(SpeechApiFactory.SynthesesPerMinute);
    }

    [Fact]
    public async Task Endpoints_Return503_WhenTheSpeechServerIsDown()
    {
        _factory.Speech.IsUp = false;
        using var form = Recording([1, 2, 3]);

        var transcribe = await _client.PostAsync(TranscribeUrl, form, Ct);
        var speak = await _client.PostAsJsonAsync(SpeakUrl, new { text = "Hello there." }, Ct);

        transcribe.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        speak.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Status_ListsTheVoices_WhenBothServersAreUp()
    {
        var status = await _client.GetFromJsonAsync<JsonElement>(StatusUrl, Ct);

        status.GetProperty("transcription").GetBoolean().Should().BeTrue();
        status.GetProperty("synthesis").GetBoolean().Should().BeTrue();
        status.GetProperty("defaultVoice").GetString().Should().Be("af_heart");
        status.GetProperty("voices").EnumerateArray().Select(v => v.GetProperty("id").GetString()).Should().Equal("af_heart", "am_adam");
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

        status.GetProperty("transcription").GetBoolean().Should().BeFalse();
        status.GetProperty("synthesis").GetBoolean().Should().BeFalse();
        status.GetProperty("voices").GetArrayLength().Should().Be(0);
        transcribe.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        speak.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        _factory.Speech.Requests.Should().BeEmpty();
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
