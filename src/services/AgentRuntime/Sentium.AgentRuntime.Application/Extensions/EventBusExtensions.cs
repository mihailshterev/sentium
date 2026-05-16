using Sentium.AgentRuntime.Core.Agents;
using Sentium.Infrastructure.Messaging;
using NATS.Client.Serializers.Json;
using Sentium.AgentRuntime.Core.Workflows;

namespace Sentium.AgentRuntime.Application.Extensions;

public static class EventBusExtensions
{
    public static Task StreamAgentUpdateAsync(this IEventBus bus, string type, string author, string text, CancellationToken ct = default)
        => StreamAgentUpdateAsync(bus, type, author, text, AgentUpdateTypes.Message, ct);

    public static Task StreamAgentUpdateAsync(this IEventBus bus, string type, string author, string text, string updateType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.PublishAsync($"stream.{type}", new AgentStreamUpdate(author, text, updateType), serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
    }
}
