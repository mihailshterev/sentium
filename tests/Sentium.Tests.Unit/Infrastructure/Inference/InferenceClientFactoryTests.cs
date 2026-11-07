using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sentium.Infrastructure.Inference;
using Xunit;

namespace Sentium.Tests.Unit.Infrastructure.Inference;

/// <summary>
/// The factory must talk to any OpenAI-compatible server (Ollama's /v1, llama.cpp's llama-server):
/// the right endpoint per client, the /v1 base, the bearer key, and no Ollama-only options.
/// </summary>
public sealed class InferenceClientFactoryTests
{
    private const string ChatClientName = "test-llm";
    private const string EmbeddingClientName = "test-embeddings";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string ChatCompletion = """
        {"id":"c1","object":"chat.completion","created":1,"model":"model-a",
         "choices":[{"index":0,"message":{"role":"assistant","content":"ALIGNED"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
        """;

    private const string EmbeddingList = """
        {"object":"list","model":"embedder",
         "data":[{"object":"embedding","index":0,"embedding":[0.25,0.5,0.75]}],
         "usage":{"prompt_tokens":1,"total_tokens":1}}
        """;

    [Fact]
    public async Task CreateChatClient_PostsToTheV1ChatCompletionsOfTheChatEndpoint()
    {
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, new()
        {
            ["Inference:ChatEndpoint"] = "http://llm.test:8080",
            ["Inference:EmbeddingEndpoint"] = "http://embeddings.test:8081",
            ["Inference:ApiKey"] = "s3cret",
        });

        using var client = services.GetRequiredService<IInferenceClientFactory>().CreateChatClient(ChatClientName, "model-a");
        var response = await client.GetResponseAsync("hello", new ChatOptions { Temperature = 0f }, Ct);

        response.Text.Should().Be("ALIGNED");
        handler.Requests.Should().ContainSingle();

        var request = handler.Requests[0];
        request.Uri.Should().Be(new Uri("http://llm.test:8080/v1/chat/completions"));
        request.Authorization.Should().Be("Bearer s3cret");

        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("model").GetString().Should().Be("model-a");
        request.Body.Should().NotContain("num_ctx", "per-request context size is Ollama-only and not part of the OpenAI API");
    }

    [Theory]
    [InlineData("http://llm.test", "http://llm.test/v1/chat/completions")]
    [InlineData("http://llm.test/", "http://llm.test/v1/chat/completions")]
    [InlineData("http://llm.test/v1", "http://llm.test/v1/chat/completions")]
    [InlineData("http://llm.test/v1/", "http://llm.test/v1/chat/completions")]
    public async Task CreateChatClient_AppendsV1OnlyWhenTheEndpointLacksIt(string endpoint, string expected)
    {
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, new() { ["Inference:ChatEndpoint"] = endpoint });

        using var client = services.GetRequiredService<IInferenceClientFactory>().CreateChatClient(ChatClientName, "model-a");
        await client.GetResponseAsync("hello", cancellationToken: Ct);

        handler.Requests.Single().Uri.Should().Be(new Uri(expected));
    }

    [Fact]
    public async Task CreateChatClient_SendsAPlaceholderKey_WhenNoApiKeyIsConfigured()
    {
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, new() { ["Inference:ChatEndpoint"] = "http://llm.test" });

        using var client = services.GetRequiredService<IInferenceClientFactory>().CreateChatClient(ChatClientName, "model-a");
        await client.GetResponseAsync("hello", cancellationToken: Ct);

        handler.Requests.Single().Authorization.Should().Be("Bearer unused");
    }

    [Fact]
    public async Task CreateChatClient_BindsEachClientToItsOwnModel()
    {
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, new() { ["Inference:ChatEndpoint"] = "http://llm.test" });
        var factory = services.GetRequiredService<IInferenceClientFactory>();

        using var first = factory.CreateChatClient(ChatClientName, "model-a");
        using var second = factory.CreateChatClient(ChatClientName, "model-b");
        await first.GetResponseAsync("hello", cancellationToken: Ct);
        await second.GetResponseAsync("hello", cancellationToken: Ct);

        handler.Requests.Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("model").GetString())
            .Should().Equal("model-a", "model-b");
    }

    [Fact]
    public async Task CreateChatClient_LetsChatOptionsModelIdOverrideTheDefaultModel()
    {
        // Sentinel's IntentCheckModel and per-agent model selection rely on this per-request override.
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, new() { ["Inference:ChatEndpoint"] = "http://llm.test" });

        using var client = services.GetRequiredService<IInferenceClientFactory>().CreateChatClient(ChatClientName, "default-model");
        await client.GetResponseAsync("hello", new ChatOptions { ModelId = "override-model" }, Ct);

        using var body = JsonDocument.Parse(handler.Requests.Single().Body);
        body.RootElement.GetProperty("model").GetString().Should().Be("override-model");
    }

    // Ollama's /v1 streams thinking as "reasoning"; llama-server (and DeepSeek) as "reasoning_content",
    // which the adapter maps natively. Both must surface exactly once, so the chat shows thought blocks.
    [Theory]
    [InlineData("reasoning")]
    [InlineData("reasoning_content")]
    public async Task CreateChatClient_StreamsTheServersReasoningAsReasoningContent(string field)
    {
        static string Chunk(string delta, string? finish = null) =>
            $$"""data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"m","choices":[{"index":0,"delta":{{delta}},"finish_reason":{{(finish is null ? "null" : $"\"{finish}\"")}}}]}""" + "\n\n";

        var sse = Chunk($$"""{"role":"assistant","content":"","{{field}}":"Think"}""")
                  + Chunk($$"""{"content":"","{{field}}":"ing."}""")
                  + Chunk("""{"content":"Yes"}""")
                  + Chunk("{}", "stop")
                  // Trailing usage chunk with no choices, as Ollama and llama-server send it.
                  + """data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"m","choices":[],"usage":{"prompt_tokens":1,"completion_tokens":2,"total_tokens":3}}""" + "\n\n"
                  + "data: [DONE]\n\n";

        using var handler = new RecordingHandler(sse, "text/event-stream");
        using var services = BuildServices(handler, new() { ["Inference:ChatEndpoint"] = "http://llm.test" });
        using var client = services.GetRequiredService<IInferenceClientFactory>().CreateChatClient(ChatClientName, "m");

        var reasoning = new StringBuilder();
        var text = new StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync("Is 17 prime?", cancellationToken: Ct))
        {
            foreach (var r in update.Contents.OfType<TextReasoningContent>())
            {
                reasoning.Append(r.Text);
            }

            text.Append(update.Text);
        }

        reasoning.ToString().Should().Be("Thinking.");
        text.ToString().Should().Be("Yes");
    }

    [Theory]
    [InlineData("reasoning")]
    [InlineData("reasoning_content")]
    public async Task CreateChatClient_ReturnsTheServersReasoningAsReasoningContent(string field)
    {
        var completion = $$"""
            {"id":"c1","object":"chat.completion","created":1,"model":"m",
             "choices":[{"index":0,"message":{"role":"assistant","content":"Yes","{{field}}":"Thinking."},"finish_reason":"stop"}]}
            """;

        using var handler = new RecordingHandler(completion);
        using var services = BuildServices(handler, new() { ["Inference:ChatEndpoint"] = "http://llm.test" });
        using var client = services.GetRequiredService<IInferenceClientFactory>().CreateChatClient(ChatClientName, "m");

        var response = await client.GetResponseAsync("Is 17 prime?", cancellationToken: Ct);

        response.Messages.SelectMany(m => m.Contents).OfType<TextReasoningContent>()
            .Should().ContainSingle().Which.Text.Should().Be("Thinking.");
        response.Text.Should().Be("Yes");
    }

    [Fact]
    public async Task CreateEmbeddingGenerator_PostsToTheV1EmbeddingsOfTheEmbeddingEndpoint()
    {
        using var handler = new RecordingHandler(EmbeddingList);
        using var services = BuildServices(handler, new()
        {
            ["Inference:ChatEndpoint"] = "http://llm.test:8080",
            ["Inference:EmbeddingEndpoint"] = "http://embeddings.test:8081",
        });

        using var generator = services.GetRequiredService<IInferenceClientFactory>().CreateEmbeddingGenerator(EmbeddingClientName, "embedder");
        var embeddings = await generator.GenerateAsync(["some text"], cancellationToken: Ct);

        embeddings.Should().ContainSingle();
        embeddings[0].Vector.ToArray().Should().Equal(0.25f, 0.5f, 0.75f);

        var request = handler.Requests.Single();
        request.Uri.Should().Be(new Uri("http://embeddings.test:8081/v1/embeddings"));
        JsonDocument.Parse(request.Body).RootElement.GetProperty("model").GetString().Should().Be("embedder");
    }

    [Fact]
    public void CreateChatClient_Throws_ForAClientNotRegisteredForInference()
    {
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, []);

        var act = () => services.GetRequiredService<IInferenceClientFactory>().CreateChatClient("unknown", "model-a");

        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown*");
    }

    [Theory]
    [InlineData("Inference:AttemptTimeout", "01:00:00")]
    [InlineData("Inference:Timeout", "00:00:00")]
    [InlineData("Inference:ChatEndpoint", "ftp://llm.test")]
    [InlineData("Inference:DefaultModel", "")]
    public void Options_FailValidation_ForInvalidSettings(string key, string value)
    {
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, new()
        {
            ["Inference:Timeout"] = "00:30:00",
            [key] = value,
        });

        var act = () => services.GetRequiredService<IOptions<InferenceOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Options_DefaultToALocalServer_WhenTheSectionIsMissing()
    {
        using var handler = new RecordingHandler(ChatCompletion);
        using var services = BuildServices(handler, []);

        var options = services.GetRequiredService<IOptions<InferenceOptions>>().Value;

        options.ChatEndpoint.Should().NotBeNull();
        options.DefaultModel.Should().NotBeNullOrWhiteSpace();
        options.AttemptTimeout.Should().BeLessThanOrEqualTo(options.Timeout);
    }

    private static ServiceProvider BuildServices(RecordingHandler handler, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();

        services.AddInferenceHttpClient(configuration, ChatClientName, InferenceEndpoint.Chat)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddInferenceHttpClient(configuration, EmbeddingClientName, InferenceEndpoint.Embedding)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    private sealed record RecordedRequest(Uri Uri, string? Authorization, string Body);

    /// <summary>
    /// Records every request and answers with a fixed body (JSON, or an SSE stream for streaming tests).
    /// Each test owns (and disposes) its own instance; disposing it twice through the HttpClient handler
    /// chain is harmless.
    /// </summary>
    private sealed class RecordingHandler(string responseBody, string mediaType = "application/json") : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedRequest(request.RequestUri!, request.Headers.Authorization?.ToString(), body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
            };
        }
    }
}
