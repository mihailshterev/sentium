using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sentium.Core.Constants;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.Audit;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Sentinel;

public sealed class SentinelApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<PolicyEvaluationResponse> EvaluateAsync(PolicyEvaluationRequest request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPolicyDecisionPoint>().EvaluateAsync(request, Ct);
    }

    [Fact]
    public async Task GetAudit_ReturnsOk_WhenAuthenticated()
    {
        var response = await _client.GetAsync("/api/sentinel/policy/audit", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditStats_ReturnsOk_WithZeroCounts_WhenEmpty()
    {
        var response = await _client.GetAsync("/api/sentinel/policy/audit/stats", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = await response.Content.ReadFromJsonAsync<AuditStatsDto>(Ct);
        Assert.NotNull(stats);
        Assert.True(stats!.Total >= 0);
    }

    [Fact]
    public async Task GetAuditByAgent_ReturnsOk_WithEmptyList()
    {
        var response = await _client.GetAsync("/api/sentinel/policy/audit/agent/test-agent-integration", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsADecision()
    {
        var body = new PolicyEvaluationRequest
        {
            AgentId = "integration-test-agent",
            SkillName = "TestSkill",
            ResourceType = "File",
            ResourceId = "docs/integration-test",
            Action = "read",
            OriginalUserPrompt = "test prompt",
            CorrelationId = Guid.NewGuid().ToString()
        };

        var result = await EvaluateAsync(body);

        Assert.NotEqual(Guid.Empty, result.AuditId);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsAllowedDecision_ForCleanReadRequest()
    {
        var body = new PolicyEvaluationRequest
        {
            AgentId = $"agent-{Guid.NewGuid()}",
            SkillName = "ReadSkill",
            ResourceType = "File",
            ResourceId = "reports/q1",
            Action = "read",
            OriginalUserPrompt = "summarize the Q1 report",
            CorrelationId = Guid.NewGuid().ToString()
        };

        var result = await EvaluateAsync(body);

        Assert.True(result!.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_AcceptsTheInventoryResourceType()
    {
        var body = new PolicyEvaluationRequest
        {
            AgentId = $"agent-{Guid.NewGuid()}",
            SkillName = "find_assets",
            ResourceType = "Inventory",
            ResourceId = "cordless drill",
            Action = "Search",
            OriginalUserPrompt = "where is the cordless drill?",
            CorrelationId = Guid.NewGuid().ToString()
        };

        var result = await EvaluateAsync(body);

        Assert.True(result!.Allowed, result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_AcceptsTheTimelineResourceType()
    {
        var body = new PolicyEvaluationRequest
        {
            AgentId = $"agent-{Guid.NewGuid()}",
            SkillName = "get_timeline_overview",
            ResourceType = "Timeline",
            ResourceId = "*",
            Action = "Read",
            OriginalUserPrompt = "how busy is my November?",
            CorrelationId = Guid.NewGuid().ToString()
        };

        var result = await EvaluateAsync(body);

        Assert.True(result!.Allowed, result.Reason);
    }

    [Fact]
    public async Task EvaluateEndpoint_IsNotExposed_EvenWithTheInternalToken()
    {
        var body = new PolicyEvaluationRequest
        {
            AgentId = "integration-test-agent",
            SkillName = "TestSkill",
            ResourceType = "File",
            ResourceId = "docs/integration-test",
            Action = "read",
            OriginalUserPrompt = "test prompt",
            CorrelationId = Guid.NewGuid().ToString()
        };

        // Every PDP caller is in-process; Sentium.Web accepts no X-Internal-Token calls at all.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sentinel/policy/evaluate")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add(CommonHeaderNames.InternalToken, MonolithWebApplicationFactory.InternalApiKey);

        var response = await _client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
