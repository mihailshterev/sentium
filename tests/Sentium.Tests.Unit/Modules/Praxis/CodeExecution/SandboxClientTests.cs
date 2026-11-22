using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Infrastructure.Realtime;
using Sentium.Praxis.Contracts;
using Sentium.Praxis.Features.CodeExecution;
using Sentium.Sentinel.Contracts;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.CodeExecution;

/// <summary>
/// Praxis authorizes every run with Sentinel before it reaches the Sandbox, and records the execution history
/// (the Sandbox is stateless).
/// </summary>
public sealed class SandboxClientTests : IDisposable
{
    private readonly IExecutionLogRepository _executionLog = Substitute.For<IExecutionLogRepository>();
    private readonly IPolicyDecisionPoint _pdp = Substitute.For<IPolicyDecisionPoint>();
    private readonly ServiceProvider _services;
    private readonly StubHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly SandboxClient _sut;

    public SandboxClientTests()
    {
        _services = new ServiceCollection()
            .AddScoped(_ => _executionLog)
            .BuildServiceProvider();

        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://sandbox/") };
        _sut = new SandboxClient(
            _httpClient,
            _pdp,
            _services.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IRealtimePublisher>(),
            NullLogger<SandboxClient>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _services.Dispose();
    }

    private static SandboxExecutionRequest MakeRequest(string code = "print('hi')", string language = "Python")
    {
        return new()
        {
            Language = language,
            Code = code,
            AgentId = "agent-1",
            OriginalUserPrompt = "say hi"
        };
    }

    private static SandboxRunResponse MakeRun()
    {
        return new()
        {
            Succeeded = true,
            ExitCode = 0,
            Output = "hi\n",
            Error = string.Empty,
            TimedOut = false,
            JobId = Guid.NewGuid(),
            DurationMs = 42
        };
    }

    private void PdpReturns(bool allowed, Guid auditId, string reason = "")
    {
        _pdp.EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PolicyEvaluationResponse
            {
                Allowed = allowed,
                Effect = allowed ? "Allow" : "Deny",
                Reason = reason,
                Risk = "Low",
                AuditId = auditId,
                Timestamp = DateTimeOffset.UtcNow
            });
    }

    [Fact]
    public async Task ExecuteAsync_RecordsSuccessfulRun_WithTheSentinelAuditId()
    {
        var ct = TestContext.Current.CancellationToken;
        var auditId = Guid.NewGuid();
        PdpReturns(allowed: true, auditId);
        var run = MakeRun();
        _handler.Respond(HttpStatusCode.OK, run);

        var result = await _sut.ExecuteAsync(MakeRequest(), "corr-1", ct);

        Assert.Equal(run.JobId, result.JobId);
        Assert.Equal(auditId, result.SentinelAuditId);
        Assert.False(result.PolicyDenied);
        await _executionLog.Received(1).AddAsync(
            Arg.Is<ExecutionLogEntry>(e => e.JobId == run.JobId && e.Succeeded && e.AgentId == "agent-1" && e.CorrelationId == "corr-1"
                && e.Language == ExecutionLanguage.Python && e.SentinelAuditId == auditId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_EvaluatesTheSandboxSkill_WithTheCodeAsPayload()
    {
        var ct = TestContext.Current.CancellationToken;
        PdpReturns(allowed: true, Guid.NewGuid());
        _handler.Respond(HttpStatusCode.OK, MakeRun());

        await _sut.ExecuteAsync(MakeRequest(language: "node", code: "console.log(1)"), "corr-2", ct);

        await _pdp.Received(1).EvaluateAsync(
            Arg.Is<PolicyEvaluationRequest>(r => r.SkillName == "sandbox.execute" && r.ResourceType == "Code" && r.ResourceId == "Node"
                && r.Action == "execute" && r.AgentId == "agent-1" && r.CorrelationId == "corr-2" && r.OriginalUserPrompt == "say hi"
                && r.Metadata["payload"] == "console.log(1)" && r.Metadata["language"] == "Node"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_CapsThePayloadSentToSentinel()
    {
        var ct = TestContext.Current.CancellationToken;
        PdpReturns(allowed: true, Guid.NewGuid());
        _handler.Respond(HttpStatusCode.OK, MakeRun());

        await _sut.ExecuteAsync(MakeRequest(code: new string('x', 10_000)), "corr-3", ct);

        await _pdp.Received(1).EvaluateAsync(
            Arg.Is<PolicyEvaluationRequest>(r => r.Metadata["payload"].Length == 8 * 1024 && r.Metadata["codeLength"] == "10000"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCallTheSandbox_AndRecordsTheDenial_WhenSentinelDenies()
    {
        var ct = TestContext.Current.CancellationToken;
        var auditId = Guid.NewGuid();
        PdpReturns(allowed: false, auditId, reason: "blocked");

        var result = await _sut.ExecuteAsync(MakeRequest(), "corr-4", ct);

        Assert.True(result.PolicyDenied);
        Assert.False(result.Succeeded);
        Assert.Equal("blocked", result.PolicyDenialReason);
        Assert.Equal(auditId, result.SentinelAuditId);
        Assert.Null(_handler.LastRequest);
        await _executionLog.Received(1).AddAsync(
            Arg.Is<ExecutionLogEntry>(e => e.PolicyDenied && e.PolicyDenialReason == "blocked" && e.SentinelAuditId == auditId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAnUnknownLanguage_BeforeAskingSentinel()
    {
        var ct = TestContext.Current.CancellationToken;

        var act = async () => await _sut.ExecuteAsync(MakeRequest(language: "ruby"), "corr-6", ct);

        Assert.Equal(HttpStatusCode.BadRequest, (await Assert.ThrowsAnyAsync<SandboxRejectedException>(act)).StatusCode);
        await _pdp.DidNotReceive().EvaluateAsync(Arg.Any<PolicyEvaluationRequest>(), Arg.Any<CancellationToken>());
        await _executionLog.DidNotReceive().AddAsync(Arg.Any<ExecutionLogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Throws_AndRecordsNothing_WhenSandboxRejects()
    {
        var ct = TestContext.Current.CancellationToken;
        PdpReturns(allowed: true, Guid.NewGuid());
        _handler.Respond(HttpStatusCode.BadRequest, new { title = "One or more validation errors occurred." });

        var act = async () => await _sut.ExecuteAsync(MakeRequest(), "corr-7", ct);

        Assert.Equal(HttpStatusCode.BadRequest, (await Assert.ThrowsAnyAsync<SandboxRejectedException>(act)).StatusCode);
        await _executionLog.DidNotReceive().AddAsync(Arg.Any<ExecutionLogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsCorrelationIdHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        PdpReturns(allowed: true, Guid.NewGuid());
        _handler.Respond(HttpStatusCode.OK, MakeRun());

        await _sut.ExecuteAsync(MakeRequest(), "corr-8", ct);

        Assert.Equal("corr-8", Assert.Single(_handler.LastRequest!.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("/execute", _handler.LastRequest.RequestUri!.AbsolutePath);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private object? _body;

        public HttpRequestMessage? LastRequest { get; private set; }

        public void Respond(HttpStatusCode status, object body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status) { Content = JsonContent.Create(_body) });
        }
    }
}
