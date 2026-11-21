using Sentium.Infrastructure.Inference;

namespace Sentium.Vigil.Features.Inference;

internal sealed record InferenceModelState(
    InferenceEndpoint Server,
    string Model,
    string Status,
    bool LoadFailed,
    int RequestsProcessing,
    int RequestsDeferred,
    DateTimeOffset UpdatedAt);
