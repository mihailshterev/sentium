using Sentium.Praxis.Features.Agents;
using Sentium.Infrastructure.Messaging;
using NATS.Client.Serializers.Json;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Extensions;

public static class EventBusExtensions
{
    public static Task StreamAgentUpdateAsync(this IEventBus bus, string streamId, string author, string text, CancellationToken ct = default)
        => StreamAgentUpdateAsync(bus, streamId, author, text, AgentUpdateTypes.Message, ct);

    public static Task StreamAgentUpdateAsync(this IEventBus bus, string streamId, string author, string text, string updateType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        return bus.PublishAsync($"stream.{streamId}", new AgentStreamUpdate(author, text, updateType), serializer: NatsJsonSerializer<AgentStreamUpdate>.Default, ct: ct);
    }
}
