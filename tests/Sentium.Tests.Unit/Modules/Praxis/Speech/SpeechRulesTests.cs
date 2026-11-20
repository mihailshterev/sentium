using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Sentium.Infrastructure.Speech;
using Sentium.Praxis.Features.Speech;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Speech;

public sealed class SpeechRulesTests
{
    private readonly SpeakRequestValidator _validator = new();

    [Theory]
    [InlineData("Hello there.", null, null)]
    [InlineData("Hello there.", "af_heart", 1.0f)]
    [InlineData("Hello there.", "am-adam_2", 0.5f)]
    [InlineData("Hello there.", null, 2.0f)]
    public void SpeakRequest_Valid(string text, string? voice, float? speed)
    {
        Assert.True(_validator.Validate(new SpeakRequest(text, voice, speed)).IsValid);
    }

    [Theory]
    [InlineData("", null, null)]
    [InlineData("   ", null, null)]
    [InlineData("Hello.", "", null)]
    [InlineData("Hello.", "../etc/passwd", null)]
    [InlineData("Hello.", "voice with spaces", null)]
    [InlineData("Hello.", null, 0.4f)]
    [InlineData("Hello.", null, 2.1f)]
    public void SpeakRequest_Invalid(string text, string? voice, float? speed)
    {
        Assert.False(_validator.Validate(new SpeakRequest(text, voice, speed)).IsValid);
    }

    [Fact]
    public void SpeakRequest_RejectsTextOverTheLimit()
    {
        var atLimit = new string('a', SpeechLimits.MaxTextLength);

        Assert.True(_validator.Validate(new SpeakRequest(atLimit)).IsValid);
        Assert.False(_validator.Validate(new SpeakRequest(atLimit + "a")).IsValid);
    }

    [Fact]
    public void ParseVoices_ReadsObjectEntries_AsKokoroFastApiReturnsThem()
    {
        using var document = JsonDocument.Parse("""
            {"voices":[
              {"id":"am_adam","name":"am_adam","target_quality":"B"},
              {"id":"af_heart","name":"Heart"},
              {"id":"af_bella"}
            ]}
            """);

        Assert.Equal(new SpeechVoice[] { new SpeechVoice("af_bella", "af_bella"), new SpeechVoice("af_heart", "Heart"), new SpeechVoice("am_adam", "am_adam") }, SpeechService.ParseVoices(document.RootElement));
    }

    [Fact]
    public void ParseVoices_ReadsPlainIds_AndSkipsUnusableEntries()
    {
        using var document = JsonDocument.Parse("""{"voices":["bf_emma","", 7, null, {"name":"no id"}, "af_heart"]}""");

        Assert.Equal(new SpeechVoice[] { new SpeechVoice("af_heart", "af_heart"), new SpeechVoice("bf_emma", "bf_emma") }, SpeechService.ParseVoices(document.RootElement));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"voices":"af_heart"}""")]
    public void ParseVoices_ReturnsNothing_ForAnUnexpectedShape(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Empty(SpeechService.ParseVoices(document.RootElement));
    }

    [Fact]
    public void SpeechOptions_DefaultsAreValid_AndSpeechIsOff()
    {
        var options = new SpeechOptions();

        Assert.Empty(Validate(options));
        Assert.False(SpeechOptions.IsConfigured(options.TranscriptionEndpoint));
        Assert.False(SpeechOptions.IsConfigured(options.SynthesisEndpoint));
    }

    [Fact]
    public void SpeechOptions_AnEmptyEndpointMeansSwitchedOff_NotInvalid()
    {
        var options = new SpeechOptions { TranscriptionEndpoint = new Uri(string.Empty, UriKind.Relative) };

        Assert.Empty(Validate(options));
        Assert.False(SpeechOptions.IsConfigured(options.TranscriptionEndpoint));
    }

    [Theory]
    [InlineData("whisper:8080", UriKind.Absolute)]
    [InlineData("/v1/audio", UriKind.Relative)]
    [InlineData("ftp://whisper:8080", UriKind.Absolute)]
    public void SpeechOptions_RejectsAnEndpointThatIsNotHttp(string endpoint, UriKind kind)
    {
        var options = new SpeechOptions { SynthesisEndpoint = new Uri(endpoint, kind) };

        Assert.Contains(nameof(SpeechOptions.SynthesisEndpoint), Assert.Single(Validate(options)).MemberNames);
    }

    [Fact]
    public void SpeechOptions_AcceptsHttpEndpoints()
    {
        var options = new SpeechOptions { TranscriptionEndpoint = new Uri("http://whisper:8080"), SynthesisEndpoint = new Uri("https://tts.local/") };

        Assert.Empty(Validate(options));
        Assert.True(SpeechOptions.IsConfigured(options.TranscriptionEndpoint));
        Assert.True(SpeechOptions.IsConfigured(options.SynthesisEndpoint));
    }

    private static List<ValidationResult> Validate(SpeechOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
