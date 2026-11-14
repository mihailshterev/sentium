using System.ComponentModel.DataAnnotations;
using Sentium.Core.Constants;

namespace Sentium.Infrastructure.Inference;

/// <summary>
/// Where the local inference servers live and how long calls to them may take. Bound from the
/// <c>Inference</c> configuration section. Both endpoints speak the OpenAI-compatible API, so the
/// runtime behind them is a deployment choice, not a code dependency.
/// </summary>
public sealed class InferenceOptions : IValidatableObject
{
    public const string SectionName = "Inference";

    /// <summary>
    /// Root URL of the server that serves the chat models, without the <c>/v1</c> suffix.
    /// </summary>
    [Required]
    public Uri ChatEndpoint { get; set; } = new("http://localhost:8090");

    /// <summary>
    /// Root URL of the server that serves the embedding model, without the <c>/v1</c> suffix.
    /// </summary>
    [Required]
    public Uri EmbeddingEndpoint { get; set; } = new("http://localhost:8091");

    /// <summary>
    /// Bearer token the inference servers require. Leave empty when they run without authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Chat model used when neither the Registry settings nor the caller name one.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DefaultModel { get; set; } = AIModels.Gemma4_E4B;

    /// <summary>
    /// Upper bound for a single inference call. Token generation on local hardware can be slow.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Upper bound for one attempt of an inference call. Must not exceed <see cref="Timeout"/>.
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(10);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Timeout <= TimeSpan.Zero)
        {
            yield return new ValidationResult("Inference:Timeout must be positive.", [nameof(Timeout)]);
        }

        if (AttemptTimeout <= TimeSpan.Zero || AttemptTimeout > Timeout)
        {
            yield return new ValidationResult("Inference:AttemptTimeout must be positive and not exceed Inference:Timeout.", [nameof(AttemptTimeout)]);
        }

        foreach (var (name, endpoint) in new[] { (nameof(ChatEndpoint), ChatEndpoint), (nameof(EmbeddingEndpoint), EmbeddingEndpoint) })
        {
            if (endpoint is { IsAbsoluteUri: false } || endpoint is { IsAbsoluteUri: true } && endpoint.Scheme is not ("http" or "https"))
            {
                yield return new ValidationResult($"Inference:{name} must be an absolute http(s) URL.", [name]);
            }
        }
    }
}
