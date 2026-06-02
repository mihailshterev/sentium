using NATS.Client.Core;
using Sentium.Infrastructure.Messaging;

namespace Sentium.Tests.Unit.Common;

internal sealed class SpyEventBus : IEventBus
{
    public List<string> PublishedSubjects { get; } = [];

    public Task PublishAsync<T>(string subject, T message, INatsSerializer<T>? serializer = null, CancellationToken ct = default)
    {
        PublishedSubjects.Add(subject);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(string subject, Func<NatsMsg<T>, Task> handler, CancellationToken ct = default) => Task.CompletedTask;
    public IAsyncEnumerable<NatsMsg<T>> SubscribeStreamAsync<T>(string subject, INatsSerializer<T>? serializer = null, CancellationToken ct = default) => AsyncEnumerable.Empty<NatsMsg<T>>();
}
