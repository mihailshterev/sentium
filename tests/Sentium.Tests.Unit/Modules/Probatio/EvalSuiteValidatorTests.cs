using System.Text.Json;
using FluentAssertions;
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

        validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AcceptsASuiteWithoutCases()
        => validator.Validate(new CreateEvalSuiteRequest("Empty for now", null, null)).IsValid.Should().BeTrue();

    [Fact]
    public void RejectsUnknownToolNames_InExpectedCallsAndCannedResults()
    {
        var expected = validator.Validate(Suite(Case(
            expectedToolCalls: [new ExpectedToolCall("delete_everything", null)],
            checks: new EvalCheck(EvalCheckType.ToolsCalled))));
        var canned = validator.Validate(Suite(Case(cannedToolResults: [new CannedToolResult("delete_everything", "ok")])));

        expected.Errors.Should().Contain(e => e.ErrorMessage.Contains("Unknown tool 'delete_everything'"));
        canned.Errors.Should().Contain(e => e.ErrorMessage.Contains("Unknown tool 'delete_everything'"));
    }

    [Fact]
    public void RejectsUnknownCheckTypes()
        => validator.Validate(Suite(Case(checks: new EvalCheck((EvalCheckType)99)))).Errors
            .Should().Contain(e => e.ErrorMessage == "Unknown Check type.");

    [Fact]
    public void RejectsACaseWithoutChecks()
    {
        var request = Suite(new EvalCaseInput(null, null, "Hi", null, null, null, null, []));

        validator.Validate(request).Errors.Should().Contain(e => e.ErrorMessage == "A case needs at least one Check.");
    }

    [Theory]
    [InlineData(nameof(EvalCheckType.ContainsExpectedAnswer))]
    [InlineData(nameof(EvalCheckType.Completeness))]
    public void RejectsAnswerChecks_WithoutAnExpectedAnswer(string type)
        => validator.Validate(Suite(Case(checks: new EvalCheck(Enum.Parse<EvalCheckType>(type))))).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(nameof(EvalCheckType.ToolsCalled))]
    [InlineData(nameof(EvalCheckType.ToolArgumentsMatch))]
    public void RejectsToolChecks_WithoutExpectedToolCalls(string type)
        => validator.Validate(Suite(Case(checks: new EvalCheck(Enum.Parse<EvalCheckType>(type))))).IsValid.Should().BeFalse();

    [Fact]
    public void RejectsGroundedness_WithoutCannedToolResults()
    {
        validator.Validate(Suite(Case(checks: new EvalCheck(EvalCheckType.Groundedness)))).IsValid.Should().BeFalse();
        validator.Validate(Suite(Case(cannedToolResults: [new CannedToolResult(KnownTool, "milk")], checks: new EvalCheck(EvalCheckType.Groundedness))))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void RejectsThresholds_OnDeterministicChecks_AndOutOfRange()
    {
        validator.Validate(Suite(Case(expectedAnswer: "milk", checks: new EvalCheck(EvalCheckType.ContainsExpectedAnswer, 3))))
            .IsValid.Should().BeFalse();
        validator.Validate(Suite(Case(checks: new EvalCheck(EvalCheckType.Relevance, 6)))).IsValid.Should().BeFalse();
        validator.Validate(Suite(Case(checks: new EvalCheck(EvalCheckType.Relevance, 3)))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RejectsDuplicateChecks_AndDuplicateCaseIds()
    {
        validator.Validate(Suite(Case(checks: [new EvalCheck(EvalCheckType.Relevance), new EvalCheck(EvalCheckType.Relevance)])))
            .IsValid.Should().BeFalse();

        var id = Guid.NewGuid();
        validator.Validate(Suite(Case() with { Id = id }, Case() with { Id = id })).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RejectsBlankPrompts_AndBlankNames()
    {
        validator.Validate(Suite(Case(prompt: "  "))).IsValid.Should().BeFalse();
        validator.Validate(new CreateEvalSuiteRequest(" ", null, null)).IsValid.Should().BeFalse();
    }
}
