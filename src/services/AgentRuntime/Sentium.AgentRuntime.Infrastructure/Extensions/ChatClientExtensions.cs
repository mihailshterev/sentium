using Microsoft.Extensions.AI;
using Sentium.AgentRuntime.Core.Learnings;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.AgentRuntime.Infrastructure.Agents;
using Sentium.AgentRuntime.Infrastructure.Sentinel;

namespace Sentium.AgentRuntime.Infrastructure.Extensions;

public static class ChatClientExtensions
{
    /// <summary>
    /// Attaches the standard Sentium middleware pipeline to a raw chat client.
    /// </summary>
    public static IChatClient AddSentiumPipeline(this IChatClient innerClient)
    {
        return new ChatClientBuilder(innerClient)
            .UseFunctionInvocation()
            .UseOpenTelemetry()
            .Build();
    }

    /// <summary>
    /// Wraps the given chat client in a harness driven by the acting user's Registry settings.
    /// </summary>
    public static IChatClient AsHarnessed(this IChatClient client, IRegistrySettingsService registrySettingsService, IPdpContextAccessor pdpContext)
        => new HarnessedChatClient(client, registrySettingsService, pdpContext);

    /// <summary>
    /// Wraps the given chat client so that relevant prior agent learnings are recalled and injected
    /// into the system prompt on each turn. Apply as the outermost decorator.
    /// </summary>
    public static IChatClient WithLearnings(this IChatClient client, IAgentLearningService learningService, IPdpContextAccessor pdpContext)
        => new LearningAugmentedChatClient(client, learningService, pdpContext);
}
