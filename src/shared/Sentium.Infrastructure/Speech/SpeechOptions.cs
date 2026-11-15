using System.ComponentModel.DataAnnotations;

namespace Sentium.Infrastructure.Speech;

/// <summary>
/// Where the local speech servers live and what a single request may cost. Bound from the <c>Speech</c>
/// configuration section. Both servers speak the OpenAI-compatible audio API. Speech is optional: an endpoint
/// left empty switches that half off, and the portal then hides its voice controls.
/// </summary>
public sealed class SpeechOptions : IValidatableObject
{
    public const string SectionName = "Speech";

    /// <summary>
    /// Root URL of the speech-to-text server, without the <c>/v1</c> suffix. Empty disables transcription.
    /// </summary>
    public Uri? TranscriptionEndpoint { get; set; }

    /// <summary>
    /// Root URL of the text-to-speech server, without the <c>/v1</c> suffix. Empty disables synthesis.
    /// </summary>
    public Uri? SynthesisEndpoint { get; set; }

    /// <summary>
    /// Sent as the request's <c>model</c>. whisper.cpp serves the one model it was started with and ignores it.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string TranscriptionModel { get; set; } = "whisper-1";

    [Required(AllowEmptyStrings = false)]
    public string SynthesisModel { get; set; } = "kokoro";

    /// <summary>
    /// The Voice used when a user has not chosen one.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DefaultVoice { get; set; } = "af_heart";

    /// <summary>
    /// Language of the recordings, as an ISO 639-1 code.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Language { get; set; } = "en";

    /// <summary>
    /// Transcriptions one user may request per minute.
    /// </summary>
    [Range(1, 10_000)]
    public int TranscriptionsPerMinute { get; set; } = 30;

    /// <summary>
    /// Synthesis requests one user may make per minute. A spoken answer is one request per sentence.
    /// </summary>
    [Range(1, 10_000)]
    public int SynthesesPerMinute { get; set; } = 240;

    /// <summary>
    /// Upper bound for a single transcription or synthesis call.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Timeout <= TimeSpan.Zero)
        {
            yield return new ValidationResult("Speech:Timeout must be positive.", [nameof(Timeout)]);
        }

        foreach (var (name, endpoint) in new[] { (nameof(TranscriptionEndpoint), TranscriptionEndpoint), (nameof(SynthesisEndpoint), SynthesisEndpoint) })
        {
            if (!IsEmpty(endpoint) && !IsConfigured(endpoint))
            {
                yield return new ValidationResult($"Speech:{name} must be an absolute http(s) URL or empty.", [name]);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="endpoint"/> names a server. An environment variable set to an empty string binds
    /// to an empty relative URI, which counts as not configured.
    /// </summary>
    public static bool IsConfigured(Uri? endpoint) => endpoint is { IsAbsoluteUri: true, Scheme: "http" or "https" };

    private static bool IsEmpty(Uri? endpoint) => endpoint is null || endpoint.OriginalString.Length == 0;
}
