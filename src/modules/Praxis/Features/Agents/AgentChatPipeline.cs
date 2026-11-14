// Compaction is experimental in Agent Framework 1.27 (MAAI001); its use is confined to this file.
#pragma warning disable MAAI001

using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace Sentium.Praxis.Features.Agents;

/// <summary>
/// Assembles the per-agent chat pipeline on top of the shared, singleton base client (OpenTelemetry → inference server):
/// <c>FunctionInvocation → Compaction → base</c>. The two per-agent layers are cheap, resource-free decorators;
/// telemetry instruments (ActivitySource/Meter) live only on the shared base, so nothing is created or leaked per request.
/// </summary>
internal static class AgentChatPipeline
{
    /// <summary>
    /// Upper bound on model round-trips in one tool-calling loop. Small local models occasionally loop on a tool;
    /// this caps the cost of that well below Microsoft.Extensions.AI's default of 40.
    /// </summary>
    private const int MaxToolIterationsPerRequest = 10;

    /// <summary>
    /// Collapse older tool call/result groups into one-line summaries once the request passes this share of the window.
    /// </summary>
    private const double ToolResultCompactionShare = 0.40;

    /// <summary>
    /// Emergency backstop: drop the oldest groups once the request passes this share of the window.
    /// </summary>
    private const double TruncationShare = 0.75;

    private const int MinimumPreservedGroups = 4;

    /// <param name="baseClient">The shared agent base client (keyed <see cref="PraxisModuleExtensions.AgentChatClientKey"/>).</param>
    /// <param name="contextWindow">Tokens one request to the agent's model may use (see <see cref="Models.IModelContextWindowProvider"/>); compaction budgets are fractions of it.</param>
    /// <param name="services">Request services - flows to tools as <see cref="AIFunctionArguments.Services"/>.</param>
    public static IChatClient Build(IChatClient baseClient, int contextWindow, ILoggerFactory loggerFactory, IServiceProvider services)
    {
        return new ChatClientBuilder(baseClient)
            .UseFunctionInvocation(loggerFactory, ficc =>
            {
                ficc.MaximumIterationsPerRequest = MaxToolIterationsPerRequest;

                // Return argument-binding errors (e.g. "missing required parameter 'code'") to the model so small
                // models can correct the call instead of seeing a generic failure. Tools return their own errors as text.
                ficc.IncludeDetailedErrors = true;
            })
            .UseAIContextProviders(CreateCompaction(contextWindow, loggerFactory))
            .Build(services);
    }

    /// <summary>
    /// Keeps the in-flight request inside the model's context window during long tool loops - essential for small local
    /// models. Sitting inside function invocation, it runs before every model call and only rewrites what is sent to the
    /// model; stored conversation history is left untouched.
    /// </summary>
    private static CompactionProvider CreateCompaction(int contextWindow, ILoggerFactory loggerFactory)
    {
        var strategy = new PipelineCompactionStrategy(
        [
            new ToolResultCompactionStrategy(CompactionTriggers.TokensExceed((int)(contextWindow * ToolResultCompactionShare))),
            new TruncationCompactionStrategy(CompactionTriggers.TokensExceed((int)(contextWindow * TruncationShare)), MinimumPreservedGroups),
        ]);

        return new CompactionProvider(strategy, loggerFactory: loggerFactory);
    }
}
