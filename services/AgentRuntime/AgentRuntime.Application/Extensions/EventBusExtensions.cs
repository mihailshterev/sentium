using AgentRuntime.Application.Workflows;
using Infrastructure.Messaging;
using NATS.Client.Serializers.Json;

namespace AgentRuntime.Application.Extensions;

public static class EventBusExtensions
{
    public static Task StreamAgentUpdateAsync(this IEventBus bus, string type, string author, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.PublishAsync($"stream.{type}", new AgentStreamUpdate(author, text), serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
    }
}
