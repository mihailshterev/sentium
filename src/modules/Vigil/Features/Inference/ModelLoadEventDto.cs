namespace Sentium.Vigil.Features.Inference;

internal sealed record ModelLoadEventDto(
    string Server,
    string Model,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    double DurationSeconds,
    bool Succeeded);
