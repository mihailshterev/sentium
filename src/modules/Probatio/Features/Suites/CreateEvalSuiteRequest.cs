namespace Sentium.Probatio.Features.Suites;

internal sealed record CreateEvalSuiteRequest(string Name, string? Description, IReadOnlyList<EvalCaseInput>? Cases) : IEvalSuiteContent;
