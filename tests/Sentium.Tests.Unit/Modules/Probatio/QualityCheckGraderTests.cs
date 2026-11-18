using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Evaluations;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Probatio;

public sealed class QualityCheckGraderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static EvalCase Case(params EvalCheck[] checks) => new(
        Guid.NewGuid(),
        null,
        "What is on the shopping list?",
        [],
        "Milk and eggs.",
        [],
        [new CannedToolResult("knowledge_base_search", "milk, eggs")],
        checks);

    private static Task<CheckOutcome> GradeAsync(FakeJudge judge, EvalCheck check)
    {
        var grader = new QualityCheckGrader(new ChatConfiguration(judge));
        List<ChatMessage> request = [new(ChatRole.User, "What is on the shopping list?")];
        var answer = new ChatResponse(new ChatMessage(ChatRole.Assistant, "You need milk and eggs."));
        return grader.GradeAsync(check, Case(check), request, answer, Ct);
    }

    [Theory]
    [InlineData(nameof(EvalCheckType.Relevance))]
    [InlineData(nameof(EvalCheckType.Coherence))]
    [InlineData(nameof(EvalCheckType.Groundedness))]
    [InlineData(nameof(EvalCheckType.Completeness))]
    public async Task PassesAtTheDefaultThreshold_AndKeepsTheJudgesReasoning(string type)
    {
        using var judge = new FakeJudge(FakeJudge.Scored(4, "It answers the question directly."));

        var outcome = await GradeAsync(judge, new EvalCheck(Enum.Parse<EvalCheckType>(type)));

        outcome.Status.Should().Be(CheckOutcomeStatus.Passed, outcome.Reason);
        outcome.Score.Should().Be(4);
        outcome.Threshold.Should().Be(4);
        outcome.Reason.Should().Contain("answers the question directly");
    }

    [Fact]
    public async Task FailsBelowItsOwnThreshold()
    {
        using var judge = new FakeJudge(FakeJudge.Scored(4, "Fine."));
        var outcome = await GradeAsync(judge, new EvalCheck(EvalCheckType.Relevance, 5));

        outcome.Status.Should().Be(CheckOutcomeStatus.Failed);
        outcome.Threshold.Should().Be(5);
    }

    [Fact]
    public async Task GivesTheJudgeTheGroundingAndTheGroundTruth()
    {
        using var groundedness = new FakeJudge(FakeJudge.Scored(5, "Grounded."));
        await GradeAsync(groundedness, new EvalCheck(EvalCheckType.Groundedness));
        groundedness.LastPrompt.Should().Contain("milk, eggs");

        using var completeness = new FakeJudge(FakeJudge.Scored(5, "Complete."));
        await GradeAsync(completeness, new EvalCheck(EvalCheckType.Completeness));
        completeness.LastPrompt.Should().Contain("Milk and eggs.");
    }

    [Fact]
    public async Task TheJudgeSeesEarlierTurns_NotJustTheLastQuestion()
    {
        using var judge = new FakeJudge(FakeJudge.Scored(5, "Good advice."));
        var grader = new QualityCheckGrader(new ChatConfiguration(judge));
        List<ChatMessage> request =
        [
            new(ChatRole.User, "My sourdough starter smells like nail polish remover."),
            new(ChatRole.Assistant, "That usually means it is hungry."),
            new(ChatRole.User, "So what should I do?"),
        ];

        await grader.GradeAsync(new EvalCheck(EvalCheckType.Relevance), Case(), request, new ChatResponse(new ChatMessage(ChatRole.Assistant, "Feed it.")), Ct);

        judge.LastPrompt.Should().Contain("nail polish remover").And.Contain("So what should I do?");
    }

    [Fact]
    public async Task AJudgeWithoutAUsableScore_FailsTheCheck()
    {
        using var judge = new FakeJudge("I would rather not say.");
        var outcome = await GradeAsync(judge, new EvalCheck(EvalCheckType.Coherence));

        outcome.Status.Should().Be(CheckOutcomeStatus.Failed);
        outcome.Score.Should().BeNull();
        outcome.Reason.Should().StartWith("The Judge gave no usable score");
    }

    [Fact]
    public async Task AJudgeThatThrows_FailsTheCheck()
    {
        using var judge = new FakeJudge(new HttpRequestException("connection refused"));
        var outcome = await GradeAsync(judge, new EvalCheck(EvalCheckType.Relevance));

        outcome.Status.Should().Be(CheckOutcomeStatus.Failed);
        outcome.Reason.Should().Contain("connection refused");
    }

    private sealed class FakeJudge : IChatClient
    {
        private readonly string? reply;
        private readonly Exception? failure;

        public FakeJudge(string reply) => this.reply = reply;

        public FakeJudge(Exception failure) => this.failure = failure;

        public string LastPrompt { get; private set; } = string.Empty;

        public static string Scored(int score, string reason)
            => $"<S0>Let me think it through.</S0>\n<S1>{reason}</S1>\n<S2>{score}</S2>";

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastPrompt = string.Join("\n", messages.Select(m => m.Text));
            return failure is null
                ? Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply!)))
                : Task.FromException<ChatResponse>(failure);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
