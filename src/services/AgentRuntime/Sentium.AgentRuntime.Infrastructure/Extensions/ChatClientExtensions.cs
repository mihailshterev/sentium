using Microsoft.Extensions.AI;
using Sentium.AgentRuntime.Core.Settings;
using Sentium.AgentRuntime.Infrastructure.Agents;

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
    /// Wraps the given chat client in a harness driven by the dynamic <see cref="ISystemSettingsService"/>.
    /// </summary>
    public static IChatClient AsHarnessed(this IChatClient client, ISystemSettingsService systemSettingsService)
        => new HarnessedChatClient(client, systemSettingsService);
}
