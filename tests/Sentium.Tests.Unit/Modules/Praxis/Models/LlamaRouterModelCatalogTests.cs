using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Sentium.Praxis.Features.Models;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Praxis.Models;

/// <summary>
/// The model catalog over llama-server's router API. Response shapes mirror what build b11804 returns.
/// </summary>
public sealed class LlamaRouterModelCatalogTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly StubHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly LlamaRouterModelCatalog _sut;

    public LlamaRouterModelCatalogTests()
    {
        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("http://llm.test/") };
        _sut = new LlamaRouterModelCatalog(_httpClient, NullLogger<LlamaRouterModelCatalog>.Instance);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task ListAsync_MapsIdStatusAndPerSlotContext_SortedById()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"data":[
              {"id":"gemma-4-e4b","object":"model","status":{"value":"loaded","args":["llama-server"]},
               "context_length":131072,"meta":{"n_ctx":16384,"n_ctx_train":131072}},
              {"id":"alpha","object":"model","status":{"value":"unloaded"}}
            ]}
            """);

        var models = await _sut.ListAsync(Ct);

        Assert.Equal(new CatalogModel[] { new CatalogModel("alpha", "unloaded", null), new CatalogModel("gemma-4-e4b", "loaded", 16384) }, models);
        Assert.Equal(new Uri("http://llm.test/models"), _handler.Requests.Single().Uri);
    }

    [Fact]
    public async Task ListAsync_SkipsEntriesWithoutAnId_AndDefaultsAMissingStatus()
    {
        _handler.Respond(HttpStatusCode.OK, """{"data":[{"object":"model"},{"id":"m"}]}""");

        var models = await _sut.ListAsync(Ct);

        Assert.Equal(new CatalogModel("m", "unknown", null), Assert.Single(models!));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ListAsync_ReturnsNull_WhenTheServerAnswersWithAnError(HttpStatusCode status)
    {
        _handler.Respond(status, """{"error":{"message":"nope"}}""");

        Assert.Null(await _sut.ListAsync(Ct));
    }

    [Fact]
    public async Task ListAsync_ReturnsNull_WhenTheServerIsUnreachable()
    {
        _handler.Fail(new HttpRequestException("connection refused"));

        Assert.Null(await _sut.ListAsync(Ct));
    }

    [Fact]
    public async Task ListAsync_ReturnsNull_ForMalformedJson()
    {
        _handler.Respond(HttpStatusCode.OK, "not json");

        Assert.Null(await _sut.ListAsync(Ct));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, nameof(ModelActionResult.Accepted))]
    [InlineData(HttpStatusCode.NotFound, nameof(ModelActionResult.NotFound))]
    [InlineData(HttpStatusCode.BadRequest, nameof(ModelActionResult.Conflict))]
    [InlineData(HttpStatusCode.InternalServerError, nameof(ModelActionResult.Failed))]
    public async Task LoadAsync_MapsTheRoutersAnswer(HttpStatusCode status, string expected)
    {
        _handler.Respond(status, status == HttpStatusCode.OK ? """{"success":true}""" : """{"error":{"code":400}}""");

        var result = await _sut.LoadAsync("gemma-4-e4b", Ct);

        Assert.Equal(Enum.Parse<ModelActionResult>(expected), result);
        var request = _handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("http://llm.test/models/load"), request.Uri);
        Assert.Equal("""{"model":"gemma-4-e4b"}""", request.Body);
    }

    [Fact]
    public async Task UnloadAsync_PostsToTheUnloadEndpoint()
    {
        _handler.Respond(HttpStatusCode.OK, """{"success":true}""");

        var result = await _sut.UnloadAsync("gemma-4-e4b", Ct);

        Assert.Equal(ModelActionResult.Accepted, result);
        Assert.Equal(new Uri("http://llm.test/models/unload"), _handler.Requests.Single().Uri);
    }

    [Fact]
    public async Task UnloadAsync_ReturnsFailed_WhenTheServerIsUnreachable()
    {
        _handler.Fail(new HttpRequestException("connection refused"));

        Assert.Equal(ModelActionResult.Failed, await _sut.UnloadAsync("gemma-4-e4b", Ct));
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body);

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";
        private Exception? _failure;

        public List<RecordedRequest> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public void Fail(Exception failure) => _failure = failure;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));

            if (_failure is not null)
            {
                throw _failure;
            }

            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}
