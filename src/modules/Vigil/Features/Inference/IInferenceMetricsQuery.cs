namespace Sentium.Vigil.Features.Inference;

internal interface IInferenceMetricsQuery
{
    Task<InferenceOverviewDto> GetOverviewAsync(InferenceRange range, CancellationToken ct);
}
