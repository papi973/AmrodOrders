using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Messaging;

public sealed record OutgoingMessage(Guid MessageId, string Type, string Exchange, string RoutingKey, string Payload, string? CorrelationId);

public interface IMessagePublisher
{
    Task PublishAsync(OutgoingMessage message, CancellationToken ct);
}

public sealed class RabbitMqPublisher(RabbitMqConnection connection) : IMessagePublisher
{
    private readonly SemaphoreSlim _lock = new(1, 1); // a channel must not be used concurrently
    private IChannel? _channel;

    public async Task PublishAsync(OutgoingMessage message, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_channel is not { IsOpen: true })
            {
                var conn = await connection.GetAsync(ct);
                // Publisher confirms: BasicPublishAsync only completes once the broker has the message.
                _channel = await conn.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
                await RabbitTopology.DeclareAsync(_channel, ct);
            }

            var props = new BasicProperties
            {
                Persistent = true,                          // survives broker restart (with durable queue)
                MessageId = message.MessageId.ToString(),
                CorrelationId = message.CorrelationId,
                ContentType = "application/json",
                Type = message.Type,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel.BasicPublishAsync(
                message.Exchange, message.RoutingKey, mandatory: true,
                basicProperties: props, body: Encoding.UTF8.GetBytes(message.Payload), cancellationToken: ct);
        }
        finally
        {
            _lock.Release();
        }
    }
}


