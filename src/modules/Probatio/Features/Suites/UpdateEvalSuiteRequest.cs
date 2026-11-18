namespace Sentium.Probatio.Features.Suites;

/// <summary>
/// The whole suite as edited. <paramref name="BaseVersion"/> is the version the edit started from and must still be
/// the current one. Changed cases create the next version; a name or description change alone does not.
/// </summary>
internal sealed record UpdateEvalSuiteRequest(string Name, string? Description, IReadOnlyList<EvalCaseInput>? Cases, int BaseVersion)
    : IEvalSuiteContent;
