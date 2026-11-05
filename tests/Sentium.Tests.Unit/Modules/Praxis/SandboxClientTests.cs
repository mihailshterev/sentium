using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis.Features.CodeExecution;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

/// <summary>
/// The Sandbox service is stateless; Praxis' client records the execution history.
/// </summary>
public sealed class SandboxClientTests : IDisposable
{
    private readonly IExecutionLogRepository _executionLog = Substitute.For<IExecutionLogRepository>();
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
        _sut = new SandboxClient(_httpClient, _services.GetRequiredService<IServiceScopeFactory>(), NullLogger<SandboxClient>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
        _services.Dispose();
    }

    private static SandboxExecutionRequest MakeRequest() => new()
    {
        Language = "Python",
        Code = "print('hi')",
        AgentId = "agent-1",
        OriginalUserPrompt = "say hi"
    };

    private static SandboxExecutionResponse MakeResponse(bool policyDenied = false) => new()
    {
        Succeeded = !policyDenied,
        ExitCode = policyDenied ? -1 : 0,
        Output = policyDenied ? string.Empty : "hi\n",
        Error = string.Empty,
        TimedOut = false,
        PolicyDenied = policyDenied,
        PolicyDenialReason = policyDenied ? "blocked" : null,
        JobId = Guid.NewGuid(),
        SentinelAuditId = Guid.NewGuid(),
        DurationMs = 42
    };

    [Fact]
    public async Task ExecuteAsync_RecordsSuccessfulRun()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = MakeResponse();
        _handler.Respond(HttpStatusCode.OK, response);

        var result = await _sut.ExecuteAsync(MakeRequest(), "corr-1", ct);

        result.JobId.Should().Be(response.JobId);
        await _executionLog.Received(1).AddAsync(
            Arg.Is<ExecutionLogEntry>(e => e.JobId == response.JobId && e.Succeeded && e.AgentId == "agent-1" && e.CorrelationId == "corr-1" && e.Language == ExecutionLanguage.Python),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RecordsPolicyDeniedRun()
    {
        var ct = TestContext.Current.CancellationToken;
        _handler.Respond(HttpStatusCode.OK, MakeResponse(policyDenied: true));

        var result = await _sut.ExecuteAsync(MakeRequest(), "corr-2", ct);

        result.PolicyDenied.Should().BeTrue();
        await _executionLog.Received(1).AddAsync(
            Arg.Is<ExecutionLogEntry>(e => e.PolicyDenied && e.PolicyDenialReason == "blocked"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Throws_AndRecordsNothing_WhenSandboxRejects()
    {
        var ct = TestContext.Current.CancellationToken;
        _handler.Respond(HttpStatusCode.BadRequest, new { title = "One or more validation errors occurred." });

        var act = async () => await _sut.ExecuteAsync(MakeRequest(), "corr-3", ct);

        (await act.Should().ThrowAsync<SandboxRejectedException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await _executionLog.DidNotReceive().AddAsync(Arg.Any<ExecutionLogEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsCorrelationIdHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        _handler.Respond(HttpStatusCode.OK, MakeResponse());

        await _sut.ExecuteAsync(MakeRequest(), "corr-4", ct);

        _handler.LastRequest!.Headers.GetValues("X-Correlation-ID").Should().ContainSingle().Which.Should().Be("corr-4");
        _handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/execute");
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
