using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sentium.Core.Constants;
using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Dtos;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Modules.Sentinel;

public sealed class SentinelApiTests(MonolithWebApplicationFactory factory) : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly HttpClient _sandboxClient = CreateSandboxClient(factory);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// The evaluate endpoint exists only for the out-of-process Sandbox, which authenticates with X-Internal-Token.
    /// </summary>
    private static HttpClient CreateSandboxClient(MonolithWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CommonHeaderNames.InternalToken, MonolithWebApplicationFactory.InternalApiKey);
        return client;
    }

    [Fact]
    public async Task GetAudit_ReturnsOk_WhenAuthenticated()
    {
        var response = await _client.GetAsync("/api/sentinel/policy/audit", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAuditStats_ReturnsOk_WithZeroCounts_WhenEmpty()
    {
        var response = await _client.GetAsync("/api/sentinel/policy/audit/stats", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<AuditStatsDto>(Ct);
        stats.Should().NotBeNull();
        stats!.Total.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetAuditByAgent_ReturnsOk_WithEmptyList()
    {
        var response = await _client.GetAsync("/api/sentinel/policy/audit/agent/test-agent-integration", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsOk_ForInternalCaller()
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

        var response = await _sandboxClient.PostAsJsonAsync("/api/sentinel/policy/evaluate", body, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PolicyEvaluationResponse>(Ct);
        result.Should().NotBeNull();
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

        var response = await _sandboxClient.PostAsJsonAsync("/api/sentinel/policy/evaluate", body, Ct);
        var result = await response.Content.ReadFromJsonAsync<PolicyEvaluationResponse>(Ct);

        result!.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsUnauthorized_WithoutInternalToken()
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

        // A signed-in user is not enough: only the Sandbox (internal token) may call the PDP over HTTP.
        var response = await _client.PostAsJsonAsync("/api/sentinel/policy/evaluate", body, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
