using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Infrastructure.Inference;
using Sentium.Vigil.Features.Inference;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil.Inference;

// HttpClient / handler lifetimes are intentionally short-lived test fixtures.
#pragma warning disable CA2000

public sealed class LlamaMetricsClientTests
{
    private const string Metrics = """
        llamacpp:prompt_tokens_total 10
        llamacpp:prompt_seconds_total 1
        llamacpp:tokens_predicted_total 5
        llamacpp:tokens_predicted_seconds_total 1
        llamacpp:requests_processing 1
        llamacpp:requests_deferred 0
        """;

    private static (LlamaMetricsClient Client, RecordingHandler Handler) Build(HttpStatusCode status, string body)
    {
        var handler = new RecordingHandler(status, body);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler) { BaseAddress = new Uri("http://llm/") });
        return (new LlamaMetricsClient(factory, NullLogger<LlamaMetricsClient>.Instance), handler);
    }

    [Fact]
    public async Task ListModels_ReadsRouterStatusesAndFailures()
    {
        var (client, _) = Build(HttpStatusCode.OK, """
            {"data":[
              {"id":"gemma","status":{"value":"loaded","args":[]}},
              {"id":"qwen","status":{"value":"unloaded","failed":true,"exit_code":1}}
            ]}
            """);

        var models = await client.ListModelsAsync(InferenceEndpoint.Chat, CancellationToken.None);

        Assert.Equal(
            [new ServedModel("gemma", "loaded", IsRouted: true, Failed: false), new ServedModel("qwen", "unloaded", IsRouted: true, Failed: true)],
            models);
    }

    [Fact]
    public async Task ListModels_TreatsAModelWithoutStatusAsALoadedSingleModelServer()
    {
        var (client, _) = Build(HttpStatusCode.OK, """{"data":[{"id":"embeddinggemma","object":"model"}]}""");

        var model = Assert.Single((await client.ListModelsAsync(InferenceEndpoint.Embedding, CancellationToken.None))!);

        Assert.True(model.IsLoaded);
        Assert.False(model.IsRouted);
    }

    [Fact]
    public async Task ListModels_IsNull_WhenTheServerAnswersWithAnError()
    {
        var (client, _) = Build(HttpStatusCode.ServiceUnavailable, "");

        Assert.Null(await client.ListModelsAsync(InferenceEndpoint.Chat, CancellationToken.None));
    }

    [Fact]
    public async Task GetSnapshot_AsksTheRouterForTheModel()
    {
        var (client, handler) = Build(HttpStatusCode.OK, Metrics);

        var snapshot = await client.GetSnapshotAsync(InferenceEndpoint.Chat, new ServedModel("gemma 4", "loaded", true, false), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.RequestsProcessing);
        Assert.Equal("/metrics?model=gemma%204", handler.LastRequest!.PathAndQuery);
    }

    [Fact]
    public async Task GetSnapshot_UsesThePlainEndpoint_ForASingleModelServer()
    {
        var (client, handler) = Build(HttpStatusCode.OK, Metrics);

        await client.GetSnapshotAsync(InferenceEndpoint.Embedding, new ServedModel("embeddinggemma", "loaded", false, false), CancellationToken.None);

        Assert.Equal("/metrics", handler.LastRequest!.PathAndQuery);
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) });
        }
    }
}
