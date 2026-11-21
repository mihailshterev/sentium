namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// One bucket of a model's series. The throughputs are tokens per second of actual processing time and are
/// <see langword="null"/> when the model did no such work in the bucket.
/// </summary>
internal sealed record InferencePointDto(
    DateTimeOffset At,
    double? PromptTokensPerSecond,
    double? GenerationTokensPerSecond,
    long PromptTokens,
    long GeneratedTokens,
    int PeakRequestsProcessing,
    int PeakRequestsDeferred,
    double BusyPercent);
