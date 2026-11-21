namespace Sentium.Vigil.Features.Inference;

/// <summary>
/// One model's buckets in time order. Buckets without samples are omitted, so a gap means no data, not zero.
/// </summary>
internal sealed record InferenceSeriesDto(string Server, string Model, IReadOnlyList<InferencePointDto> Points);
