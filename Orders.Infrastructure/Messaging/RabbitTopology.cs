using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Messaging;

public static class RabbitTopology
{
    public const string OrdersExchange = "orders.events";
    public const string OrderCreatedRoutingKey = "order.created";
    public const string OrderCreatedQueue = "orders.order-created";

    public const string DeadLetterExchange = "orders.dlx";
    public const string DeadLetterRoutingKey = "order.created.dead";
    public const string DeadLetterQueue = "orders.order-created.dlq";

    /* Exponential-ish backoff: each retry queue holds the message for its TTL, then dead-letters it
    back to the main exchange. No consumers on these queues.*/
    public static readonly (string Name, TimeSpan Delay)[] RetryQueues =
    [
        ("orders.order-created.retry.5s", TimeSpan.FromSeconds(5)),
        ("orders.order-created.retry.30s", TimeSpan.FromSeconds(30)),
        ("orders.order-created.retry.2m", TimeSpan.FromMinutes(2)),
    ];

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(OrdersExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(OrderCreatedQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,       // nack(requeue:false) → DLQ
                ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
            },
            cancellationToken: ct);
        await channel.QueueBindAsync(OrderCreatedQueue, OrdersExchange, OrderCreatedRoutingKey, cancellationToken: ct);

        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey, cancellationToken: ct);

        foreach (var (name, delay) in RetryQueues)
        {
            await channel.QueueDeclareAsync(name, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = (int)delay.TotalMilliseconds,
                    ["x-dead-letter-exchange"] = OrdersExchange,
                    ["x-dead-letter-routing-key"] = OrderCreatedRoutingKey
                },
                cancellationToken: ct);
        }
    }
}
