using System.Text.Json;
using NSubstitute;
using Sentium.Praxis.Contracts;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Suites;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Probatio;

public sealed class EvalSuiteValidatorTests
{
    private const string KnownTool = "knowledge_base_search";

    private readonly CreateEvalSuiteRequestValidator validator;

    public EvalSuiteValidatorTests()
    {
        var catalog = Substitute.For<IToolCatalog>();
        catalog.Contains(Arg.Any<string>()).Returns(call => string.Equals(call.Arg<string>(), KnownTool, StringComparison.OrdinalIgnoreCase));
        validator = new CreateEvalSuiteRequestValidator(catalog);
    }

    private static EvalCaseInput Case(
        string prompt = "What is on the shopping list?",
        string? expectedAnswer = null,
        IReadOnlyList<ExpectedToolCall>? expectedToolCalls = null,
        IReadOnlyList<CannedToolResult>? cannedToolResults = null,
        params EvalCheck[] checks)
    {
        return new EvalCaseInput(
            null,
            null,
            prompt,
            null,
            expectedAnswer,
            expectedToolCalls,
            cannedToolResults,
            checks.Length == 0 ? [new EvalCheck(EvalCheckType.Relevance)] : checks);
    }

    private static CreateEvalSuiteRequest Suite(params EvalCaseInput[] cases) => new("Shopping", null, cases);

    [Fact]
    public void AcceptsAFullyAuthoredCase()
    {
        var arguments = new Dictionary<string, JsonElement> { ["query"] = JsonSerializer.SerializeToElement("milk") };
        var request = Suite(Case(
            expectedAnswer: "milk",
            expectedToolCalls: [new ExpectedToolCall(KnownTool, arguments)],
            cannedToolResults: [new CannedToolResult(KnownTool, "milk, eggs")],
            checks:
            [
                new EvalCheck(EvalCheckType.ContainsExpectedAnswer),
                new EvalCheck(EvalCheckType.ToolArgumentsMatch),
                new EvalCheck(EvalCheckType.Completeness, 5),
            ]));

        Assert.True(validator.Validate(request).IsValid);
    }

    [Fact]
    public void AcceptsASuiteWithoutCases()
        => Assert.True(validator.Validate(new CreateEvalSuiteRequest("Empty for now", null, null)).IsValid);

    [Fact]
    public void RejectsUnknownToolNames_InExpectedCallsAndCannedResults()
    {
        var expected = validator.Validate(Suite(Case(
            expectedToolCalls: [new ExpectedToolCall("delete_everything", null)],
            checks: new EvalCheck(EvalCheckType.ToolsCalled))));
        var canned = validator.Validate(Suite(Case(cannedToolResults: [new CannedToolResult("delete_everything", "ok")])));

        Assert.Contains(expected.Errors, e => e.ErrorMessage.Contains("Unknown tool 'delete_everything'"));
        Assert.Contains(canned.Errors, e => e.ErrorMessage.Contains("Unknown tool 'delete_everything'"));
    }

    [Fact]
    public void RejectsUnknownCheckTypes()
        => Assert.Contains(validator.Validate(Suite(Case(checks: new EvalCheck((EvalCheckType)99)))).Errors, e => e.ErrorMessage == "Unknown Check type.");

    [Fact]
    public void RejectsACaseWithoutChecks()
    {
        var request = Suite(new EvalCaseInput(null, null, "Hi", null, null, null, null, []));

        Assert.Contains(validator.Validate(request).Errors, e => e.ErrorMessage == "A case needs at least one Check.");
    }

    [Theory]
    [InlineData(nameof(EvalCheckType.ContainsExpectedAnswer))]
    [InlineData(nameof(EvalCheckType.Completeness))]
    public void RejectsAnswerChecks_WithoutAnExpectedAnswer(string type)
        => Assert.False(validator.Validate(Suite(Case(checks: new EvalCheck(Enum.Parse<EvalCheckType>(type))))).IsValid);

    [Theory]
    [InlineData(nameof(EvalCheckType.ToolsCalled))]
    [InlineData(nameof(EvalCheckType.ToolArgumentsMatch))]
    public void RejectsToolChecks_WithoutExpectedToolCalls(string type)
        => Assert.False(validator.Validate(Suite(Case(checks: new EvalCheck(Enum.Parse<EvalCheckType>(type))))).IsValid);

    [Fact]
    public void RejectsGroundedness_WithoutCannedToolResults()
    {
        Assert.False(validator.Validate(Suite(Case(checks: new EvalCheck(EvalCheckType.Groundedness)))).IsValid);
        Assert.True(validator.Validate(Suite(Case(cannedToolResults: [new CannedToolResult(KnownTool, "milk")], checks: new EvalCheck(EvalCheckType.Groundedness))))
            .IsValid);
    }

    [Fact]
    public void RejectsThresholds_OnDeterministicChecks_AndOutOfRange()
    {
        Assert.False(validator.Validate(Suite(Case(expectedAnswer: "milk", checks: new EvalCheck(EvalCheckType.ContainsExpectedAnswer, 3))))
            .IsValid);
        Assert.False(validator.Validate(Suite(Case(checks: new EvalCheck(EvalCheckType.Relevance, 6)))).IsValid);
        Assert.True(validator.Validate(Suite(Case(checks: new EvalCheck(EvalCheckType.Relevance, 3)))).IsValid);
    }

    [Fact]
    public void RejectsDuplicateChecks_AndDuplicateCaseIds()
    {
        Assert.False(validator.Validate(Suite(Case(checks: [new EvalCheck(EvalCheckType.Relevance), new EvalCheck(EvalCheckType.Relevance)])))
            .IsValid);

        var id = Guid.NewGuid();
        Assert.False(validator.Validate(Suite(Case() with { Id = id }, Case() with { Id = id })).IsValid);
    }

    [Fact]
    public void RejectsBlankPrompts_AndBlankNames()
    {
        Assert.False(validator.Validate(Suite(Case(prompt: "  "))).IsValid);
        Assert.False(validator.Validate(new CreateEvalSuiteRequest(" ", null, null)).IsValid);
    }
}
