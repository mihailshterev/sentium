namespace Sentium.Vigil.Features.Inference;

internal sealed record InferenceModelStateDto(
    string Server,
    string Model,
    string Status,
    bool LoadFailed,
    int RequestsProcessing,
    int RequestsDeferred,
    DateTimeOffset UpdatedAt);
