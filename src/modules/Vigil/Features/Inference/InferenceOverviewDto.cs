namespace Sentium.Vigil.Features.Inference;

internal sealed record InferenceOverviewDto(
    string Range,
    int StepMinutes,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<InferenceModelStateDto> Models,
    IReadOnlyList<InferenceSeriesDto> Series,
    IReadOnlyList<InferenceUsageDto> Usage,
    IReadOnlyList<ModelLoadEventDto> Loads);
