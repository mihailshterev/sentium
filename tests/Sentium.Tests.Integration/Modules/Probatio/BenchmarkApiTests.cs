using System.Net;
using System.Net.Http.Json;
using Sentium.Core.Results;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Benchmarks;
using Sentium.Probatio.Features.Evaluations;
using Sentium.Probatio.Features.Suites;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Probatio;

/// <summary>
/// Benchmarks end to end: one Evaluation per model of the same suite version and agent, against the scripted
/// models of <see cref="ProbatioApiFactory"/>.
/// </summary>
public sealed class BenchmarkApiTests : IClassFixture<ProbatioApiFactory>
{
    private const string Benchmarks = "/api/probatio/benchmarks";
    private const string Agent = "ResearchAnalyst";
    private const string Tool = ProbatioApiFactory.ScriptedModel.Tool;

    private readonly ProbatioApiFactory factory;

    public BenchmarkApiTests(ProbatioApiFactory factory)
    {
        this.factory = factory;
        factory.Script.Reset();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string[] BothModels => [ProbatioApiFactory.Model, ProbatioApiFactory.OtherModel];

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private static async Task<EvalSuiteDto> CreateSuiteAsync(HttpClient client)
    {
        var shopping = new EvalCaseInput(
            null,
            "Shopping",
            "What is on the shopping list?",
            null,
            "milk",
            [new ExpectedToolCall(Tool, null)],
            [new CannedToolResult(Tool, "milk, eggs")],
            [new EvalCheck(EvalCheckType.ContainsExpectedAnswer), new EvalCheck(EvalCheckType.ToolsCalled), new EvalCheck(EvalCheckType.Relevance)]);

        var response = await client.PostAsJsonAsync("/api/probatio/suites", new CreateEvalSuiteRequest("Shopping", null, [shopping]), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<EvalSuiteDto>(Ct))!;
    }

    private static async Task<BenchmarkSummaryDto> StartAsync(HttpClient client, Guid suiteId, params string[] models)
    {
        var response = await client.PostAsJsonAsync(Benchmarks, new StartBenchmarkRequest(suiteId, null, Agent, models), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<BenchmarkSummaryDto>(Ct))!;
    }

    private static async Task<BenchmarkDto> GetAsync(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<BenchmarkDto>($"{Benchmarks}/{id}", Ct))!;

    private async Task ProcessAsync(BenchmarkSummaryDto benchmark)
    {
        foreach (var evaluation in benchmark.Evaluations)
        {
            await factory.Worker.ProcessAsync(evaluation.Id, Ct);
        }
    }

    [Fact]
    public async Task Benchmark_EvaluatesEveryModel_OnOneSuiteVersion_WithOneJudge()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client);

        var started = await StartAsync(client, suite.Id, BothModels);

        Assert.Equal(BothModels, started.Evaluations.Select(e => e.Model));
        Assert.All(started.Evaluations, e => Assert.True(e.BenchmarkId == started.Id && e.SuiteVersion == suite.Version && e.AgentName == Agent && e.Status == EvaluationStatus.Queued));
        Assert.Equal(ProbatioApiFactory.DefaultModel, started.JudgeModel);
        Assert.All(started.Evaluations, e => Assert.True(e.JudgeModel == started.JudgeModel));

        await ProcessAsync(started);
        var benchmark = await GetAsync(client, started.Id);

        Assert.Equal(BothModels, benchmark.Evaluations.Select(e => e.Summary.Model));
        Assert.All(benchmark.Evaluations, e => Assert.True(e.Summary.Status == EvaluationStatus.Completed && e.Summary.PassedCaseCount == 1 && e.ModelDeleted == false));
        Assert.All(benchmark.Evaluations, e => Assert.True(e.Cases.Single().Repetitions.Single().LatencyMs >= 0));
        Assert.Equivalent(BothModels, factory.Script.Requests.Select(r => r.ModelId).Distinct());
        Assert.All(factory.Script.JudgeModels, m => Assert.True(m == ProbatioApiFactory.DefaultModel));

        var list = await client.GetFromJsonAsync<PagedResponse<BenchmarkSummaryDto>>(Benchmarks, Ct);
        Assert.Equal(2, Assert.Single(list!.Items, b => b.Id == started.Id).Evaluations.Count);
    }

    [Fact]
    public async Task DeletedModels_AreShownAsDeleted()
    {
        var client = ClientFor(Guid.NewGuid());
        var started = await StartAsync(client, (await CreateSuiteAsync(client)).Id, BothModels);

        factory.ServedModels.Remove(ProbatioApiFactory.OtherModel);
        try
        {
            var benchmark = await GetAsync(client, started.Id);
            Assert.False(benchmark.Evaluations.Single(e => e.Summary.Model == ProbatioApiFactory.Model).ModelDeleted);
            Assert.True(benchmark.Evaluations.Single(e => e.Summary.Model == ProbatioApiFactory.OtherModel).ModelDeleted);

            var other = started.Evaluations.Single(e => e.Model == ProbatioApiFactory.OtherModel);
            Assert.True((await client.GetFromJsonAsync<EvaluationDto>($"/api/probatio/evaluations/{other.Id}", Ct))!.ModelDeleted);
        }
        finally
        {
            factory.ServedModels.Add(ProbatioApiFactory.OtherModel);
        }
    }

    [Fact]
    public async Task Start_NeedsTwoToSixDistinctUsableModels_AndQueuesNothingOtherwise()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateSuiteAsync(client);

        string[][] invalid =
        [
            [ProbatioApiFactory.Model],
            [ProbatioApiFactory.Model, ProbatioApiFactory.Model],
            [ProbatioApiFactory.Model, "missing-model"],
        ];

        foreach (var models in invalid)
        {
            var response = await client.PostAsJsonAsync(Benchmarks, new StartBenchmarkRequest(suite.Id, null, Agent, models), Ct);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest, string.Join(", ", models));
        }

        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<BenchmarkSummaryDto>>(Benchmarks, Ct))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<PagedResponse<EvaluationSummaryDto>>("/api/probatio/evaluations", Ct))!.Items);
    }

    [Fact]
    public async Task Benchmark_CanBeCancelled_ThenDeletedWithItsEvaluations()
    {
        var client = ClientFor(Guid.NewGuid());
        var started = await StartAsync(client, (await CreateSuiteAsync(client)).Id, BothModels);

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"{Benchmarks}/{started.Id}", Ct)).StatusCode);

        var cancel = await client.PostAsync($"{Benchmarks}/{started.Id}/cancel", null, Ct);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.All((await cancel.Content.ReadFromJsonAsync<BenchmarkSummaryDto>(Ct))!.Evaluations, e => Assert.True(e.Status == EvaluationStatus.Cancelled));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"{Benchmarks}/{started.Id}/cancel", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Benchmarks}/{started.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Benchmarks}/{started.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/probatio/evaluations/{started.Evaluations[0].Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Benchmarks_ArePrivate_EvenFromTheSovereign()
    {
        var owner = ClientFor(Guid.NewGuid());
        var started = await StartAsync(owner, (await CreateSuiteAsync(owner)).Id, BothModels);

        foreach (var client in new[] { ClientFor(Guid.NewGuid()), factory.CreateClient() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Benchmarks}/{started.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"{Benchmarks}/{started.Id}/cancel", null, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Benchmarks}/{started.Id}", Ct)).StatusCode);
            Assert.DoesNotContain((await client.GetFromJsonAsync<PagedResponse<BenchmarkSummaryDto>>(Benchmarks, Ct))!.Items, b => b.Id == started.Id);
        }
    }
}
