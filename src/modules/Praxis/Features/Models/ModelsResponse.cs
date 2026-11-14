namespace Sentium.Praxis.Features.Models;

/// <summary>
/// The declared chat models plus the effective default model (Registry setting, else configuration).
/// </summary>
internal sealed record ModelsResponse(string DefaultModel, IReadOnlyList<ModelInfo> Models);
