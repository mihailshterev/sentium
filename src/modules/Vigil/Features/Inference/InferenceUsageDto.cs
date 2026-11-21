namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// Totals over the range for one caller, model and operation. The averages are <see langword="null"/> when no
/// request reported one (time to first token exists only for streamed requests).
/// </summary>
internal sealed record InferenceUsageDto(
    string Caller,
    string Model,
    string Operation,
    long Requests,
    long Errors,
    long InputTokens,
    long OutputTokens,
    double? AverageDurationSeconds,
    double? AverageTimeToFirstTokenSeconds);
