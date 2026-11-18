using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Results;
using Sentium.Infrastructure.Security;
using Sentium.Probatio.Data;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Evaluations;
using Sentium.Probatio.Features.Suites;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Probatio;

/// <summary>
/// Evaluations end to end through the real agent build, tool stubbing and grading, against a scripted model
/// (see <see cref="ProbatioApiFactory.ScriptedModel"/>).
/// </summary>
public sealed class EvaluationApiTests : IClassFixture<ProbatioApiFactory>
{
    private const string Evaluations = "/api/probatio/evaluations";
    private const string Agent = "ResearchAnalyst";
    private const string Tool = ProbatioApiFactory.ScriptedModel.Tool;

    private readonly ProbatioApiFactory factory;

    public EvaluationApiTests(ProbatioApiFactory factory)
    {
        this.factory = factory;
        factory.Script.Reset();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private static EvalCaseInput ShoppingCase() => new(
        null,
        "Shopping",
        "What is on the shopping list?",
        [new EvalTurn(EvalTurnRole.User, "Hi"), new EvalTurn(EvalTurnRole.Assistant, "Hello!")],
        "milk",
        [new ExpectedToolCall(Tool, new Dictionary<string, System.Text.Json.JsonElement> { ["query"] = System.Text.Json.JsonSerializer.SerializeToElement("milk") })],
        [new CannedToolResult(Tool, "milk, eggs")],
        [
            new EvalCheck(EvalCheckType.ContainsExpectedAnswer),
            new EvalCheck(EvalCheckType.ToolsCalled),
            new EvalCheck(EvalCheckType.ToolArgumentsMatch),
            new EvalCheck(EvalCheckType.Relevance),
        ]);

    private static EvalCaseInput GreetingCase() => new(
        null,
        "Greeting",
        "Say something",
        null,
        "milk",
        null,
        null,
        [new EvalCheck(EvalCheckType.ContainsExpectedAnswer)]);

    private static async Task<EvalSuiteDto> CreateSuiteAsync(HttpClient client, params EvalCaseInput[] cases)
    {
        var response = await client.PostAsJsonAsync("/api/probatio/suites", new CreateEvalSuiteRequest("Shopping", null, cases), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<EvalSuiteDto>(Ct))!;
    }

    private static async Task<EvaluationSummaryDto> StartAsync(HttpClient client, Guid suiteId, int repetitions = 1)
    {
        var response = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(suiteId, null, Agent, ProbatioApiFactory.Model, repetitions), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<EvaluationSummaryDto>(Ct))!;
    }

    private static async Task<EvaluationDto> GetAsync(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<EvaluationDto>($"{Evaluations}/{id}", Ct))!;

    [Fact]
    public async Task Evaluation_StubsTools_GradesEveryRepetition_AndRecordsMetrics()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, ShoppingCase(), GreetingCase());
        var started = await StartAsync(client, suite.Id, repetitions: 2);

        started.Status.Should().Be(EvaluationStatus.Queued);
        started.CaseCount.Should().Be(2);

        await factory.Worker.ProcessAsync(started.Id, Ct);
        var evaluation = await GetAsync(client, started.Id);

        evaluation.Summary.Status.Should().Be(EvaluationStatus.Completed);
        evaluation.Summary.CompletedCaseCount.Should().Be(2);
        evaluation.Summary.PassedCaseCount.Should().Be(1);
        evaluation.Summary.Model.Should().Be(ProbatioApiFactory.Model);

        var shopping = evaluation.Cases[0];
        shopping.Passed.Should().BeTrue();
        shopping.PassedRepetitions.Should().Be(2);
        shopping.Repetitions.Should().HaveCount(2);

        var repetition = shopping.Repetitions[0];
        repetition.ToolCalls.Should().ContainSingle(c => c.ToolName == Tool && c.Result == "milk, eggs");
        repetition.ToolCalls[0].Arguments.GetProperty("query").GetString().Should().Be("milk");
        repetition.Transcript.Select(m => m.Role).Should().Equal("user", "assistant", "user", "assistant");
        repetition.Transcript[^1].Text.Should().Be("You still need: milk, eggs");
        repetition.Checks.Select(c => (c.Type, c.Status)).Should().Equal(
            (EvalCheckType.ContainsExpectedAnswer, CheckOutcomeStatus.Passed),
            (EvalCheckType.ToolsCalled, CheckOutcomeStatus.Passed),
            (EvalCheckType.ToolArgumentsMatch, CheckOutcomeStatus.Passed),
            (EvalCheckType.Relevance, CheckOutcomeStatus.Passed));
        repetition.OutputTokens.Should().BeGreaterThan(0);
        repetition.InputTokens.Should().BeGreaterThan(0);
        repetition.LatencyMs.Should().BeGreaterThanOrEqualTo(0);

        var greeting = evaluation.Cases[1];
        greeting.Passed.Should().BeFalse();
        greeting.Repetitions[0].Checks.Should().ContainSingle(c => c.Status == CheckOutcomeStatus.Failed);

        // The model got the canned result, so the real knowledge-base search never ran (ADR 0003).
        factory.Script.ToolResultsSeen.Should().OnlyContain(r => r == "milk, eggs").And.HaveCount(2);
        factory.Script.Requests.Should().OnlyContain(r => r.ModelId == ProbatioApiFactory.Model);
        factory.Script.Requests[0].Tools.Should().Contain(Tool);
        factory.Script.Requests[0].Instructions.Should().Contain(Tool);
    }

    [Fact]
    public async Task QualityChecks_AreScoredByTheUsersJudge_FrozenAtStart()
    {
        var client = ClientFor(Guid.NewGuid());
        (await client.PutAsJsonAsync("/api/registry/settings/evaluation", new { judgeModel = ProbatioApiFactory.JudgeModel }, Ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var suite = await CreateSuiteAsync(client, ShoppingCase() with
        {
            Checks = [new EvalCheck(EvalCheckType.Relevance), new EvalCheck(EvalCheckType.Groundedness, 5)],
        });

        var started = await StartAsync(client, suite.Id);
        started.JudgeModel.Should().Be(ProbatioApiFactory.JudgeModel);

        // Changing the setting afterwards does not change the Judge of a started Evaluation.
        await client.PutAsJsonAsync("/api/registry/settings/evaluation", new { judgeModel = (string?)null }, Ct);
        await factory.Worker.ProcessAsync(started.Id, Ct);

        var checks = (await GetAsync(client, started.Id)).Cases[0].Repetitions[0].Checks;
        checks[0].Should().Match<CheckOutcome>(c =>
            c.Status == CheckOutcomeStatus.Passed && c.Score == 4 && c.Threshold == 4 && c.Reason!.Contains("Judged by judge-model"));
        checks[1].Should().Match<CheckOutcome>(c => c.Status == CheckOutcomeStatus.Failed && c.Score == 4 && c.Threshold == 5);
        factory.Script.JudgeModels.Should().NotBeEmpty().And.OnlyContain(m => m == ProbatioApiFactory.JudgeModel);
    }

    [Fact]
    public async Task Judge_DefaultsToTheDefaultModel_AndIsOnlyNeededForQualityChecks()
    {
        var client = ClientFor(Guid.NewGuid());
        var quality = await CreateSuiteAsync(client, ShoppingCase());
        var deterministic = await CreateSuiteAsync(client, GreetingCase());

        (await StartAsync(client, quality.Id)).JudgeModel.Should().Be(ProbatioApiFactory.DefaultModel);

        await client.PutAsJsonAsync("/api/registry/settings/evaluation", new { judgeModel = "missing-judge" }, Ct);
        var unusable = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(quality.Id, null, Agent, ProbatioApiFactory.Model), Ct);
        unusable.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unusable.Content.ReadAsStringAsync(Ct)).Should().Contain("Unknown model 'missing-judge'");

        (await StartAsync(client, deterministic.Id)).JudgeModel.Should().BeNull("a suite without quality Checks needs no Judge");
    }

    [Fact]
    public async Task Start_RejectsUnknownAgents_UnknownModels_AndEmptySuites()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, GreetingCase());
        var empty = await CreateSuiteAsync(client);

        var unknownAgent = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(suite.Id, null, "Nobody", ProbatioApiFactory.Model), Ct);
        var unknownModel = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(suite.Id, null, Agent, "missing-model"), Ct);
        var emptySuite = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(empty.Id, null, Agent, ProbatioApiFactory.Model), Ct);
        var tooMany = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(suite.Id, null, Agent, ProbatioApiFactory.Model, 99), Ct);

        unknownAgent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownAgent.Content.ReadAsStringAsync(Ct)).Should().Contain("no agent named 'Nobody'");
        unknownModel.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownModel.Content.ReadAsStringAsync(Ct)).Should().Contain("Unknown model 'missing-model'");
        emptySuite.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        tooMany.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QueuedEvaluation_CanBeCancelled_ThenDeleted_ButNotCancelledTwice()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, GreetingCase());
        var started = await StartAsync(client, suite.Id);

        (await client.DeleteAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var cancel = await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancel.Content.ReadFromJsonAsync<EvaluationSummaryDto>(Ct))!.Status.Should().Be(EvaluationStatus.Cancelled);

        await factory.Worker.ProcessAsync(started.Id, Ct);
        (await GetAsync(client, started.Id)).Summary.Status.Should().Be(EvaluationStatus.Cancelled, "a cancelled Evaluation is never claimed");

        (await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RunningEvaluation_StopsWhenCancelled()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, GreetingCase());
        var started = await StartAsync(client, suite.Id);
        factory.Script.Block = true;

        var processing = factory.Worker.ProcessAsync(started.Id, Ct);
        await factory.Script.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        (await GetAsync(client, started.Id)).Summary.Status.Should().Be(EvaluationStatus.Running);

        (await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        await processing.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        var evaluation = await GetAsync(client, started.Id);
        evaluation.Summary.Status.Should().Be(EvaluationStatus.Cancelled);
        evaluation.Summary.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Evaluation_KeepsItsOwnCopy_WhenTheSuiteIsDeleted()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, ShoppingCase());
        var started = await StartAsync(client, suite.Id);

        (await client.DeleteAsync($"/api/probatio/suites/{suite.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await factory.Worker.ProcessAsync(started.Id, Ct);

        var evaluation = await GetAsync(client, started.Id);
        evaluation.Summary.Status.Should().Be(EvaluationStatus.Completed);
        evaluation.Summary.SuiteName.Should().Be("Shopping");
        evaluation.Cases.Should().ContainSingle(c => c.Prompt == "What is on the shopping list?" && c.Passed == true);
    }

    [Fact]
    public async Task Evaluations_ArePrivate_EvenFromTheSovereign()
    {
        var owner = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(owner, GreetingCase());
        var started = await StartAsync(owner, suite.Id);

        foreach (var client in new[] { ClientFor(Guid.NewGuid()), factory.CreateClient() })
        {
            (await client.GetAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.GetFromJsonAsync<PagedResponse<EvaluationSummaryDto>>(Evaluations, Ct))!.Items.Should().NotContain(e => e.Id == started.Id);
            (await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(suite.Id, null, Agent, ProbatioApiFactory.Model), Ct))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, "someone else's suite cannot be evaluated");
        }

        var page = await owner.GetFromJsonAsync<PagedResponse<EvaluationSummaryDto>>($"{Evaluations}?suiteId={suite.Id}", Ct);
        page!.Items.Should().ContainSingle(e => e.Id == started.Id);
    }

    [Fact]
    public async Task Recovery_FailsInterruptedEvaluations()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, GreetingCase());
        var started = await StartAsync(client, suite.Id);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<SystemScopeContext>().Activate();
            var db = scope.ServiceProvider.GetRequiredService<ProbatioDbContext>();
            await db.Evaluations.Where(e => e.Id == started.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EvaluationStatus.Running), Ct);
        }

        await factory.Worker.RecoverAsync(Ct);

        var evaluation = await GetAsync(client, started.Id);
        evaluation.Summary.Status.Should().Be(EvaluationStatus.Failed);
        evaluation.Summary.Error.Should().Contain("stopped before");
    }
}
