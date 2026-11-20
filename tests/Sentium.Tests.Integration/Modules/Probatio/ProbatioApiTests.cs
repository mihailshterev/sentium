using System.Net;
using System.Net.Http.Json;
using Sentium.Core.Results;
using Sentium.Probatio.Entities;
using Sentium.Probatio.Features.Suites;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Probatio;

/// <summary>
/// The Eval Suite API end to end. Each test acts as fresh Member users (see <see cref="TestAuthHandler.UserHeader"/>),
/// so isolation can be asserted, including against the Sovereign.
/// </summary>
public sealed class ProbatioApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private const string Suites = "/api/probatio/suites";
    private const string KnownTool = "knowledge_base_search";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId.ToString());
        return client;
    }

    private static EvalCaseInput Case(string prompt, Guid? id = null) => new(
        id,
        null,
        prompt,
        [new EvalTurn(EvalTurnRole.User, "Hi"), new EvalTurn(EvalTurnRole.Assistant, "Hello!")],
        "milk",
        [new ExpectedToolCall(KnownTool, null)],
        [new CannedToolResult(KnownTool, "milk, eggs")],
        [new EvalCheck(EvalCheckType.ContainsExpectedAnswer), new EvalCheck(EvalCheckType.ToolsCalled), new EvalCheck(EvalCheckType.Relevance, 3)]);

    private static async Task<EvalSuiteDto> CreateAsync(HttpClient client, params EvalCaseInput[] cases)
    {
        var response = await client.PostAsJsonAsync(Suites, new CreateEvalSuiteRequest("  Shopping  ", "List questions", cases), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<EvalSuiteDto>(Ct))!;
    }

    private static async Task<EvalSuiteDto> UpdateAsync(HttpClient client, EvalSuiteDto suite, string name, params EvalCaseInput[] cases)
    {
        var response = await client.PutAsJsonAsync($"{Suites}/{suite.Id}", new UpdateEvalSuiteRequest(name, suite.Description, cases, suite.CurrentVersion), Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<EvalSuiteDto>(Ct))!;
    }

    private static EvalCaseInput AsInput(EvalCase c)
        => new(c.Id, c.Name, c.Prompt, c.PriorTurns, c.ExpectedAnswer, c.ExpectedToolCalls, c.CannedToolResults, c.Checks);

    [Fact]
    public async Task Suite_CanBeCreatedReadListedAndDeleted()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateAsync(client, Case("What do we need?"));

        Assert.Equal("Shopping", suite.Name);
        Assert.Equal(1, suite.Version);
        Assert.Single(suite.Cases);
        Assert.NotEqual(Guid.Empty, suite.Cases[0].Id);
        Assert.Equal(3, suite.Cases[0].Checks.Count);
        Assert.Equal(2, suite.Cases[0].PriorTurns.Count);

        var fetched = await client.GetFromJsonAsync<EvalSuiteDto>($"{Suites}/{suite.Id}", Ct);
        Assert.Equivalent(suite.Cases, fetched!.Cases);

        var page = await client.GetFromJsonAsync<PagedResponse<EvalSuiteSummaryDto>>(Suites, Ct);
        Assert.Single(page!.Items, s => s.Id == suite.Id && s.CaseCount == 1 && s.CurrentVersion == 1);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Suites}/{suite.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Suites}/{suite.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Suites}/{suite.Id}/versions/1", Ct)).StatusCode);
    }

    [Fact]
    public async Task EditingCases_CreatesANewVersion_AndKeepsEarlierVersionsReadable()
    {
        var client = ClientFor(Guid.NewGuid());
        var v1 = await CreateAsync(client, Case("What do we need?"));
        var kept = v1.Cases[0];

        var v2 = await UpdateAsync(client, v1, "Shopping", AsInput(kept) with { Prompt = "What do we still need?" }, Case("Anything else?"));

        Assert.Equal(2, v2.Version);
        Assert.Equal(2, v2.CurrentVersion);
        Assert.Equal(2, v2.Cases.Count);
        // a kept case keeps its id across versions
        Assert.Equal(kept.Id, v2.Cases[0].Id);

        var old = await client.GetFromJsonAsync<EvalSuiteDto>($"{Suites}/{v1.Id}/versions/1", Ct);
        Assert.Equal(1, old!.Version);
        Assert.Equal(2, old.CurrentVersion);
        Assert.Single(old.Cases, c => c.Prompt == "What do we need?");

        var versions = await client.GetFromJsonAsync<List<EvalSuiteVersionDto>>($"{Suites}/{v1.Id}/versions", Ct);
        Assert.Equal(new (int Version, int CaseCount)[] { (2, 2), (1, 1) }, versions!.Select(v => (v.Version, v.CaseCount)));
    }

    [Fact]
    public async Task Renaming_WithoutChangingCases_DoesNotCreateAVersion()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateAsync(client, Case("What do we need?"));

        var renamed = await UpdateAsync(client, suite, "Groceries", AsInput(suite.Cases[0]));

        Assert.Equal("Groceries", renamed.Name);
        Assert.Equal(1, renamed.Version);
    }

    [Fact]
    public async Task EditingFromAStaleVersion_IsAConflict()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateAsync(client, Case("What do we need?"));
        await UpdateAsync(client, suite, "Shopping", Case("Changed"));

        var stale = await client.PutAsJsonAsync($"{Suites}/{suite.Id}", new UpdateEvalSuiteRequest("Shopping", null, [Case("Other")], BaseVersion: 1), Ct);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task UnknownTools_AndUnknownCheckTypes_AreRejected()
    {
        var client = ClientFor(Guid.NewGuid());

        var unknownTool = await client.PostAsJsonAsync(
            Suites,
            new CreateEvalSuiteRequest("Bad", null, [Case("Hi") with { ExpectedToolCalls = [new ExpectedToolCall("delete_everything", null)] }]),
            Ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknownTool.StatusCode);
        Assert.Contains("Unknown tool 'delete_everything'", await unknownTool.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        var unknownCheck = await client.PostAsJsonAsync(
            Suites,
            new { name = "Bad", cases = new[] { new { prompt = "Hi", checks = new[] { new { type = "Vibes" } } } } },
            Ct);
        Assert.Equal(HttpStatusCode.BadRequest, unknownCheck.StatusCode);
    }

    [Fact]
    public async Task Suites_ArePrivate_EvenFromTheSovereign()
    {
        var owner = ClientFor(Guid.NewGuid());
        var suite = await CreateAsync(owner, Case("What do we need?"));

        var other = ClientFor(Guid.NewGuid());
        var sovereign = factory.CreateClient();

        foreach (var client in new[] { other, sovereign })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Suites}/{suite.Id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Suites}/{suite.Id}/versions", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Suites}/{suite.Id}", Ct)).StatusCode);
            Assert.DoesNotContain((await client.GetFromJsonAsync<PagedResponse<EvalSuiteSummaryDto>>(Suites, Ct))!.Items, s => s.Id == suite.Id);
        }

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"{Suites}/{suite.Id}", Ct)).StatusCode);
    }
}
