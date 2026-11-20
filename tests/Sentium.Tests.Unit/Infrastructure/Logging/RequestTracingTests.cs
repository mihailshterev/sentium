using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentium.Core.Constants;
using Sentium.Infrastructure.Extensions;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Logging;

public sealed class RequestTracingTests
{
    private readonly ScopeCapturingLoggerProvider _logs = new();

    [Fact]
    public async Task EchoesTheCallersCorrelationId()
    {
        var context = await SendAsync(request => request.Headers[CommonHeaderNames.CorrelationId] = "caller-id");

        Assert.Equal("caller-id", context.Response.Headers[CommonHeaderNames.CorrelationId].ToString());
    }

    [Fact]
    public async Task FallsBackToTheTraceIdentifier()
    {
        var context = await SendAsync(_ => { }, traceIdentifier: "trace-42");

        Assert.Equal("trace-42", context.Response.Headers[CommonHeaderNames.CorrelationId].ToString());
    }

    [Fact]
    public async Task RunsTheRestOfThePipeline()
    {
        var reached = false;

        await SendAsync(_ => { }, next: _ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        Assert.True(reached);
    }

    [Fact]
    public async Task LogsWrittenDuringTheRequest_CarryTheCorrelationId()
    {
        await SendAsync(request => request.Headers[CommonHeaderNames.CorrelationId] = "caller-id", next: LogInsideAsync);

        var scope = Assert.Single(_logs.Entries);
        Assert.Equal("caller-id", scope[CommonHeaderNames.CorrelationId]);
    }

    private static Task LogInsideAsync(HttpContext context)
    {
        context.RequestServices.GetRequiredService<ILogger<RequestTracingTests>>().LogInformation("inside the request");
        return Task.CompletedTask;
    }

    private async Task<HttpContext> SendAsync(
        Action<HttpRequest> configure,
        RequestDelegate? next = null,
        string traceIdentifier = "trace")
    {
        await using var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(_logs))
            .BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseSentiumTracing();
        app.Run(next ?? (_ => Task.CompletedTask));

        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = traceIdentifier };
        configure(context.Request);

        await app.Build()(context);
        return context;
    }

    /// <summary>
    /// Records, for every log entry, the key/value pairs of the scopes active when it was written.
    /// </summary>
    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider, ISupportExternalScope, ILogger
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public List<Dictionary<string, object?>> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = new Dictionary<string, object?>();
            _scopes.ForEachScope(Collect, values);
            Entries.Add(values);
        }

        public void Dispose()
        {
        }

        private static void Collect(object? scope, Dictionary<string, object?> values)
        {
            if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
            {
                foreach (var (key, value) in pairs)
                {
                    values[key] = value;
                }
            }
        }
    }
}
