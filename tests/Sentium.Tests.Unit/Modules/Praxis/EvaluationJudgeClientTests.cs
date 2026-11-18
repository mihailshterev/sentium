using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis.Features.Agents.Runtime;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis;

public sealed class EvaluationJudgeClientTests
{
    [Fact]
    public async Task JudgeClient_PinsTheModel_AndLiftsTheEvaluatorsOutputCap()
    {
        using var baseClient = new RecordingClient();
        var builder = new EvaluationAgentBuilder(
            baseClient,
            Substitute.For<IAgentRoster>(),
            Substitute.For<IAgentFactory>(),
            null!,
            null!,
            null!,
            null!,
            Substitute.For<IServiceProvider>(),
            NullLoggerFactory.Instance);

        var judge = builder.CreateJudgeClient("judge-model");
        await judge.GetResponseAsync("score this", new ChatOptions { MaxOutputTokens = 800 }, TestContext.Current.CancellationToken);

        baseClient.Options!.ModelId.Should().Be("judge-model");
        baseClient.Options.MaxOutputTokens.Should().BeGreaterThan(800, "a reasoning Judge spends the evaluators' 800 tokens before it scores");
    }

    private sealed class RecordingClient : IChatClient
    {
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
