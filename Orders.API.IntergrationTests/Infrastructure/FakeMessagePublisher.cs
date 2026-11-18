using System.Collections.Concurrent;
using Orders.Infrastructure.Messaging;

namespace Orders.API.IntegrationTests.Infrastructure;

public sealed class FakeMessagePublisher : IMessagePublisher
{
    public ConcurrentQueue<OutgoingMessage> Published { get; } = new();

    public Task PublishAsync(OutgoingMessage message, CancellationToken ct)
    {
        Published.Enqueue(message);
        return Task.CompletedTask;
    }
}