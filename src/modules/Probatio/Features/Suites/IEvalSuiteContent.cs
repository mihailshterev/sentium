namespace Sentium.Probatio.Features.Suites;

/// <summary>
/// What creating and editing a suite have in common, so both are validated by <see cref="EvalSuiteContentValidator"/>.
/// </summary>
internal interface IEvalSuiteContent
{
    string Name { get; }

    string? Description { get; }

    IReadOnlyList<EvalCaseInput>? Cases { get; }
}
