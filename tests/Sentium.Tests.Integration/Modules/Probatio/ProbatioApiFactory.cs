using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sentium.Praxis;
using Sentium.Praxis.Features.Models;
using Sentium.Probatio.Features.Evaluations;
using Sentium.Tests.Integration.Common;

namespace Sentium.Tests.Integration.Modules.Probatio;

/// <summary>
/// The monolith with a scripted model behind the agent base client and a fixed model catalog, so Evaluations go
/// through the real agent build, stubbing and grading without an inference server.
/// </summary>
public sealed class ProbatioApiFactory : MonolithWebApplicationFactory
{
    public const string Model = "eval-model";
    public const string OtherModel = "eval-model-2";
    public const string JudgeModel = "judge-model";

    /// <summary>
    /// The configured default model (appsettings.Testing.json), and so the default Judge.
    /// </summary>
    public const string DefaultModel = "gemma-4-e4b";

    public ScriptedModel Script { get; } = new();

    /// <summary>
    /// What the fake model catalog serves; remove a model to make it "deleted".
    /// </summary>
    public HashSet<string> ServedModels { get; } = [Model, OtherModel, JudgeModel, DefaultModel];

    /// <summary>
    /// A worker like the hosted one (the tests remove hosted services), driven explicitly.
    /// </summary>
    internal EvaluationWorker Worker => new(
        Services.GetRequiredService<IServiceScopeFactory>(),
        Services.GetRequiredService<EvaluationQueue>(),
        Services.GetRequiredService<EvaluationCancellations>(),
        TimeProvider.System,
        NullLogger<EvaluationWorker>.Instance);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAllKeyed<IChatClient>(PraxisModuleExtensions.AgentChatClientKey);
            services.AddKeyedSingleton<IChatClient>(PraxisModuleExtensions.AgentChatClientKey, Script);

            var catalog = Substitute.For<IModelCatalog>();
            catalog.ListAsync(Arg.Any<CancellationToken>())
                .Returns(_ => Task.FromResult<IReadOnlyList<CatalogModel>?>([.. ServedModels.Select(m => new CatalogModel(m, "loaded", 8192))]));
            services.RemoveAll<IModelCatalog>();
            services.AddSingleton(catalog);
        });
    }

    /// <summary>
    /// Plays the models under evaluation (requests on <see cref="Model"/> or <see cref="OtherModel"/>) and the Judge
    /// (any other model). Under
    /// evaluation, a prompt mentioning "shopping" makes it search the knowledge base for milk and then answer with
    /// whatever the tool returned; any other prompt is answered "Hello.". As the Judge it gives
    /// <see cref="JudgeScore"/> in the quality evaluators' reply format. While <see cref="Block"/> is set it waits
    /// until the request is cancelled.
    /// </summary>
    public sealed class ScriptedModel : IChatClient
    {
        public const string Tool = "knowledge_base_search";

        private readonly Lock gate = new();

        public bool Block { get; set; }

        public int JudgeScore { get; set; } = 4;

        public List<string?> JudgeModels { get; } = [];

        public TaskCompletionSource Blocked { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(string? ModelId, string? Instructions, IReadOnlyList<string> Tools)> Requests { get; } = [];

        public List<string> ToolResultsSeen { get; } = [];

        public void Reset()
        {
            Block = false;
            JudgeScore = 4;
            Blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate)
            {
                Requests.Clear();
                ToolResultsSeen.Clear();
                JudgeModels.Clear();
            }
        }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var list = messages.ToList();
            if (options?.ModelId is not (Model or OtherModel))
            {
                lock (gate)
                {
                    JudgeModels.Add(options?.ModelId);
                }

                return Reply(new ChatMessage(ChatRole.Assistant, $"<S0>Thinking.</S0><S1>Judged by {options?.ModelId}.</S1><S2>{JudgeScore}</S2>"));
            }

            lock (gate)
            {
                Requests.Add((options?.ModelId, options?.Instructions, [.. (options?.Tools ?? []).Select(t => t.Name)]));
            }

            if (Block)
            {
                Blocked.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            var toolResult = list.LastOrDefault()?.Contents.OfType<FunctionResultContent>().FirstOrDefault();
            if (toolResult is not null)
            {
                var text = toolResult.Result?.ToString() ?? string.Empty;
                lock (gate)
                {
                    ToolResultsSeen.Add(text);
                }

                return Reply(new ChatMessage(ChatRole.Assistant, $"You still need: {text}"));
            }

            var prompt = list.Last(m => m.Role == ChatRole.User).Text;
            if (prompt.Contains("shopping", StringComparison.OrdinalIgnoreCase))
            {
                var arguments = new Dictionary<string, object?> { ["query"] = JsonSerializer.SerializeToElement("milk") };
                return Reply(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"call-{Guid.NewGuid():N}", Tool, arguments)]));
            }

            return Reply(new ChatMessage(ChatRole.Assistant, "Hello."));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Evaluations do not stream.");

        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }

        private static ChatResponse Reply(ChatMessage message)
            => new(message) { Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20 } };
    }
}
