using System.Net;
using System.Net.Http.Json;
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
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<EvalSuiteDto>(Ct))!;
    }

    private static async Task<EvaluationSummaryDto> StartAsync(HttpClient client, Guid suiteId, int repetitions = 1)
    {
        var response = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(suiteId, null, Agent, ProbatioApiFactory.Model, repetitions), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
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

        Assert.Equal(EvaluationStatus.Queued, started.Status);
        Assert.Equal(2, started.CaseCount);

        await factory.Worker.ProcessAsync(started.Id, Ct);
        var evaluation = await GetAsync(client, started.Id);

        Assert.Equal(EvaluationStatus.Completed, evaluation.Summary.Status);
        Assert.Equal(2, evaluation.Summary.CompletedCaseCount);
        Assert.Equal(1, evaluation.Summary.PassedCaseCount);
        Assert.Equal(ProbatioApiFactory.Model, evaluation.Summary.Model);

        var shopping = evaluation.Cases[0];
        Assert.True(shopping.Passed);
        Assert.Equal(2, shopping.PassedRepetitions);
        Assert.Equal(2, shopping.Repetitions.Count);

        var repetition = shopping.Repetitions[0];
        Assert.Single(repetition.ToolCalls, c => c.ToolName == Tool && c.Result == "milk, eggs");
        Assert.Equal("milk", repetition.ToolCalls[0].Arguments.GetProperty("query").GetString());
        Assert.Equal(new string[] { "user", "assistant", "user", "assistant" }, repetition.Transcript.Select(m => m.Role));
        Assert.Equal("You still need: milk, eggs", repetition.Transcript[^1].Text);
        Assert.Equal(new (EvalCheckType Type, CheckOutcomeStatus Status)[] { (EvalCheckType.ContainsExpectedAnswer, CheckOutcomeStatus.Passed), (EvalCheckType.ToolsCalled, CheckOutcomeStatus.Passed), (EvalCheckType.ToolArgumentsMatch, CheckOutcomeStatus.Passed), (EvalCheckType.Relevance, CheckOutcomeStatus.Passed) }, repetition.Checks.Select(c => (c.Type, c.Status)));
        Assert.True(repetition.OutputTokens > 0);
        Assert.True(repetition.InputTokens > 0);
        Assert.True(repetition.LatencyMs >= 0);

        var greeting = evaluation.Cases[1];
        Assert.False(greeting.Passed);
        Assert.Single(greeting.Repetitions[0].Checks, c => c.Status == CheckOutcomeStatus.Failed);

        // The model got the canned result, so the real knowledge-base search never ran (ADR 0003).
        Assert.All(factory.Script.ToolResultsSeen, r => Assert.True(r == "milk, eggs"));
        Assert.Equal(2, factory.Script.ToolResultsSeen.Count);
        Assert.All(factory.Script.Requests, r => Assert.True(r.ModelId == ProbatioApiFactory.Model));
        Assert.Contains(Tool, factory.Script.Requests[0].Tools);
        Assert.Contains(Tool, factory.Script.Requests[0].Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QualityChecks_AreScoredByTheUsersJudge_FrozenAtStart()
    {
        var client = ClientFor(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/registry/settings/evaluation", new { judgeModel = ProbatioApiFactory.JudgeModel }, Ct))
            .StatusCode);
        var suite = await CreateSuiteAsync(client, ShoppingCase() with
        {
            Checks = [new EvalCheck(EvalCheckType.Relevance), new EvalCheck(EvalCheckType.Groundedness, 5)],
        });

        var started = await StartAsync(client, suite.Id);
        Assert.Equal(ProbatioApiFactory.JudgeModel, started.JudgeModel);

        // Changing the setting afterwards does not change the Judge of a started Evaluation.
        await client.PutAsJsonAsync("/api/registry/settings/evaluation", new { judgeModel = (string?)null }, Ct);
        await factory.Worker.ProcessAsync(started.Id, Ct);

        var checks = (await GetAsync(client, started.Id)).Cases[0].Repetitions[0].Checks;
        Assert.Equal(CheckOutcomeStatus.Passed, checks[0].Status);
        Assert.Equal(4, checks[0].Score);
        Assert.Equal(4, checks[0].Threshold);
        Assert.Contains("Judged by judge-model", checks[0].Reason, StringComparison.Ordinal);
        Assert.Equal(CheckOutcomeStatus.Failed, checks[1].Status);
        Assert.Equal(4, checks[1].Score);
        Assert.Equal(5, checks[1].Threshold);
        Assert.NotEmpty(factory.Script.JudgeModels);
        Assert.All(factory.Script.JudgeModels, m => Assert.True(m == ProbatioApiFactory.JudgeModel));
    }

    [Fact]
    public async Task Judge_DefaultsToTheDefaultModel_AndIsOnlyNeededForQualityChecks()
    {
        var client = ClientFor(Guid.NewGuid());
        var quality = await CreateSuiteAsync(client, ShoppingCase());
        var deterministic = await CreateSuiteAsync(client, GreetingCase());

        Assert.Equal(ProbatioApiFactory.DefaultModel, (await StartAsync(client, quality.Id)).JudgeModel);

        await client.PutAsJsonAsync("/api/registry/settings/evaluation", new { judgeModel = "missing-judge" }, Ct);
        var unusable = await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(quality.Id, null, Agent, ProbatioApiFactory.Model), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, unusable.StatusCode);
        Assert.Contains("Unknown model 'missing-judge'", await unusable.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        // a suite without quality Checks needs no Judge
        Assert.Null((await StartAsync(client, deterministic.Id)).JudgeModel);
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

        Assert.Equal(HttpStatusCode.BadRequest, unknownAgent.StatusCode);
        Assert.Contains("no agent named 'Nobody'", await unknownAgent.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, unknownModel.StatusCode);
        Assert.Contains("Unknown model 'missing-model'", await unknownModel.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, emptySuite.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task QueuedEvaluation_CanBeCancelled_ThenDeleted_ButNotCancelledTwice()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, GreetingCase());
        var started = await StartAsync(client, suite.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode);

        var cancel = await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(EvaluationStatus.Cancelled, (await cancel.Content.ReadFromJsonAsync<EvaluationSummaryDto>(Ct))!.Status);

        await factory.Worker.ProcessAsync(started.Id, Ct);
        // a cancelled Evaluation is never claimed
        Assert.Equal(EvaluationStatus.Cancelled, (await GetAsync(client, started.Id)).Summary.Status);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode);
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
        Assert.Equal(EvaluationStatus.Running, (await GetAsync(client, started.Id)).Summary.Status);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct)).StatusCode);
        await processing.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        var evaluation = await GetAsync(client, started.Id);
        Assert.Equal(EvaluationStatus.Cancelled, evaluation.Summary.Status);
        Assert.NotNull(evaluation.Summary.FinishedAt);
    }

    [Fact]
    public async Task Evaluation_KeepsItsOwnCopy_WhenTheSuiteIsDeleted()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client, ShoppingCase());
        var started = await StartAsync(client, suite.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/probatio/suites/{suite.Id}", Ct)).StatusCode);
        await factory.Worker.ProcessAsync(started.Id, Ct);

        var evaluation = await GetAsync(client, started.Id);
        Assert.Equal(EvaluationStatus.Completed, evaluation.Summary.Status);
        Assert.Equal("Shopping", evaluation.Summary.SuiteName);
        Assert.Single(evaluation.Cases, c => c.Prompt == "What is on the shopping list?" && c.Passed == true);
    }

    [Fact]
    public async Task Evaluations_ArePrivate_EvenFromTheSovereign()
    {
        var owner = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(owner, GreetingCase());
        var started = await StartAsync(owner, suite.Id);

        foreach (var client in new[] { ClientFor(Guid.NewGuid()), factory.CreateClient() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Evaluations}/{started.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{Evaluations}/{started.Id}/cancel", null, Ct)).StatusCode);
            Assert.DoesNotContain((await client.GetFromJsonAsync<PagedResponse<EvaluationSummaryDto>>(Evaluations, Ct))!.Items, e => e.Id == started.Id);
            // someone else's suite cannot be evaluated
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Evaluations, new StartEvaluationRequest(suite.Id, null, Agent, ProbatioApiFactory.Model), Ct))
                .StatusCode);
        }

        var page = await owner.GetFromJsonAsync<PagedResponse<EvaluationSummaryDto>>($"{Evaluations}?suiteId={suite.Id}", Ct);
        Assert.Single(page!.Items, e => e.Id == started.Id);
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
        Assert.Equal(EvaluationStatus.Failed, evaluation.Summary.Status);
        Assert.Contains("stopped before", evaluation.Summary.Error, StringComparison.Ordinal);
    }
}
