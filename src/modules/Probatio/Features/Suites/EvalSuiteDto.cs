using Sentium.Probatio.Entities;

namespace Sentium.Probatio.Features.Suites;

/// <summary>
/// A suite with the cases of one version. <paramref name="Version"/> is older than <paramref name="CurrentVersion"/>
/// when an earlier version was asked for.
/// </summary>
internal sealed record EvalSuiteDto(
    Guid Id,
    string Name,
    string? Description,
    int Version,
    int CurrentVersion,
    DateTimeOffset VersionCreatedAt,
    IReadOnlyList<EvalCase> Cases,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
