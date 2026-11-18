using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<EvalSuiteDto>(Ct))!;
    }

    private static async Task<EvalSuiteDto> UpdateAsync(HttpClient client, EvalSuiteDto suite, string name, params EvalCaseInput[] cases)
    {
        var response = await client.PutAsJsonAsync($"{Suites}/{suite.Id}", new UpdateEvalSuiteRequest(name, suite.Description, cases, suite.CurrentVersion), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<EvalSuiteDto>(Ct))!;
    }

    private static EvalCaseInput AsInput(EvalCase c)
        => new(c.Id, c.Name, c.Prompt, c.PriorTurns, c.ExpectedAnswer, c.ExpectedToolCalls, c.CannedToolResults, c.Checks);

    [Fact]
    public async Task Suite_CanBeCreatedReadListedAndDeleted()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateAsync(client, Case("What do we need?"));

        suite.Name.Should().Be("Shopping");
        suite.Version.Should().Be(1);
        suite.Cases.Should().ContainSingle();
        suite.Cases[0].Id.Should().NotBeEmpty();
        suite.Cases[0].Checks.Should().HaveCount(3);
        suite.Cases[0].PriorTurns.Should().HaveCount(2);

        var fetched = await client.GetFromJsonAsync<EvalSuiteDto>($"{Suites}/{suite.Id}", Ct);
        fetched!.Cases.Should().BeEquivalentTo(suite.Cases);

        var page = await client.GetFromJsonAsync<PagedResponse<EvalSuiteSummaryDto>>(Suites, Ct);
        page!.Items.Should().ContainSingle(s => s.Id == suite.Id && s.CaseCount == 1 && s.CurrentVersion == 1);

        (await client.DeleteAsync($"{Suites}/{suite.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"{Suites}/{suite.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"{Suites}/{suite.Id}/versions/1", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditingCases_CreatesANewVersion_AndKeepsEarlierVersionsReadable()
    {
        var client = ClientFor(Guid.NewGuid());
        var v1 = await CreateAsync(client, Case("What do we need?"));
        var kept = v1.Cases[0];

        var v2 = await UpdateAsync(client, v1, "Shopping", AsInput(kept) with { Prompt = "What do we still need?" }, Case("Anything else?"));

        v2.Version.Should().Be(2);
        v2.CurrentVersion.Should().Be(2);
        v2.Cases.Should().HaveCount(2);
        v2.Cases[0].Id.Should().Be(kept.Id, "a kept case keeps its id across versions");

        var old = await client.GetFromJsonAsync<EvalSuiteDto>($"{Suites}/{v1.Id}/versions/1", Ct);
        old!.Version.Should().Be(1);
        old.CurrentVersion.Should().Be(2);
        old.Cases.Should().ContainSingle(c => c.Prompt == "What do we need?");

        var versions = await client.GetFromJsonAsync<List<EvalSuiteVersionDto>>($"{Suites}/{v1.Id}/versions", Ct);
        versions!.Select(v => (v.Version, v.CaseCount)).Should().Equal((2, 2), (1, 1));
    }

    [Fact]
    public async Task Renaming_WithoutChangingCases_DoesNotCreateAVersion()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateAsync(client, Case("What do we need?"));

        var renamed = await UpdateAsync(client, suite, "Groceries", AsInput(suite.Cases[0]));

        renamed.Name.Should().Be("Groceries");
        renamed.Version.Should().Be(1);
    }

    [Fact]
    public async Task EditingFromAStaleVersion_IsAConflict()
    {
        var client = ClientFor(Guid.NewGuid());
        var suite = await CreateAsync(client, Case("What do we need?"));
        await UpdateAsync(client, suite, "Shopping", Case("Changed"));

        var stale = await client.PutAsJsonAsync($"{Suites}/{suite.Id}", new UpdateEvalSuiteRequest("Shopping", null, [Case("Other")], BaseVersion: 1), Ct);

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnknownTools_AndUnknownCheckTypes_AreRejected()
    {
        var client = ClientFor(Guid.NewGuid());

        var unknownTool = await client.PostAsJsonAsync(
            Suites,
            new CreateEvalSuiteRequest("Bad", null, [Case("Hi") with { ExpectedToolCalls = [new ExpectedToolCall("delete_everything", null)] }]),
            Ct);
        unknownTool.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await unknownTool.Content.ReadAsStringAsync(Ct)).Should().Contain("Unknown tool 'delete_everything'");

        var unknownCheck = await client.PostAsJsonAsync(
            Suites,
            new { name = "Bad", cases = new[] { new { prompt = "Hi", checks = new[] { new { type = "Vibes" } } } } },
            Ct);
        unknownCheck.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
            (await client.GetAsync($"{Suites}/{suite.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.GetAsync($"{Suites}/{suite.Id}/versions", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.DeleteAsync($"{Suites}/{suite.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await client.GetFromJsonAsync<PagedResponse<EvalSuiteSummaryDto>>(Suites, Ct))!.Items.Should().NotContain(s => s.Id == suite.Id);
        }

        (await owner.GetAsync($"{Suites}/{suite.Id}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
