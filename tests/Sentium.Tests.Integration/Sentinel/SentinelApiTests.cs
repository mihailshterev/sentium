using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Sentium.Sentinel.Core.Dtos;
using Sentium.Tests.Integration.Common;
using Xunit;

namespace Sentium.Tests.Integration.Sentinel;

public sealed class SentinelApiTests(SentinelTestFactory factory) : IClassFixture<SentinelTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAudit_ReturnsOk_WhenAuthenticated()
    {
        var response = await _client.GetAsync("/policy/audit", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAuditStats_ReturnsOk_WithZeroCounts_WhenEmpty()
    {
        var response = await _client.GetAsync("/policy/audit/stats", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<AuditStatsDto>(Ct);
        stats.Should().NotBeNull();
        stats!.Total.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetSettings_ReturnsOk_WithDefaultSettings()
    {
        var response = await _client.GetAsync("/policy/settings", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<PdpSettingsDto>(Ct);
        settings.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSettings_ReturnsOk_WithUpdatedValues()
    {
        var body = new UpdatePdpSettingsRequest { LockdownMode = true };

        var response = await _client.PutAsJsonAsync("/policy/settings", body, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<PdpSettingsDto>(Ct);
        settings!.LockdownMode.Should().BeTrue();
    }

    [Fact]
    public async Task GetAuditByAgent_ReturnsOk_WithEmptyList()
    {
        var response = await _client.GetAsync("/policy/audit/agent/test-agent-integration", Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsOk_InDevelopmentEnvironment()
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

        var response = await _client.PostAsJsonAsync("/policy/evaluate", body, Ct);

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

        var response = await _client.PostAsJsonAsync("/policy/evaluate", body, Ct);
        var result = await response.Content.ReadFromJsonAsync<PolicyEvaluationResponse>(Ct);

        result!.Allowed.Should().BeTrue();
    }
}
