using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orders.Contracts;
using Orders.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Orders.Worker;

public sealed class OrderCreatedConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopes,
    IOptions<WorkerOptions> options,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    private const string RetryHeader = "x-retry-count";
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var conn = await ConnectWithRetryAsync(stoppingToken);

        _channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);
        await RabbitTopology.DeclareAsync(_channel, stoppingToken);

        // Backpressure: at most N unacked messages in flight for this consumer.
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: options.Value.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, ea) => HandleAsync(ea, stoppingToken);

        await _channel.BasicConsumeAsync(RabbitTopology.OrderCreatedQueue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Consuming from {Queue}", RabbitTopology.OrderCreatedQueue);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    private async Task HandleAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var channel = _channel!;
        var props = ea.BasicProperties;
        var attempt = GetRetryCount(props);
        var body = ea.Body.ToArray();

        using var _ = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = props.CorrelationId,   // same id the API logged
            ["MessageId"] = props.MessageId,
            ["Attempt"] = attempt
        });

        // Poison message: can never succeed, so don't retry; dead-letter immediately.
        if (!Guid.TryParse(props.MessageId, out var messageId) || Deserialize(body) is not { } evt)
        {
            logger.LogError("Poison message (missing MessageId or invalid body); dead-lettering");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, ct);
            return;
        }

        try
        {
            using var scope = scopes.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OrderAllocationProcessor>();
            await processor.ProcessAsync(messageId, evt, ct);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (attempt < RabbitTopology.RetryQueues.Length)
            {
                var (retryQueue, delay) = RabbitTopology.RetryQueues[attempt];
                logger.LogWarning(ex, "Processing failed; retry {Next} in {Delay}", attempt + 1, delay);
                await RepublishAsync(channel, exchange: "", routingKey: retryQueue, props, body, attempt + 1, error: null, ct);
            }
            else
            {
                logger.LogError(ex, "Processing failed after {Attempts} attempts; moving to DLQ", attempt + 1);
                await RepublishAsync(channel, RabbitTopology.DeadLetterExchange, RabbitTopology.DeadLetterRoutingKey,
                    props, body, attempt, ex.Message, ct);
            }

            // Ack the original only after the copy is safely published (confirms enabled).
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
    }

    private static async Task RepublishAsync(IChannel channel, string exchange, string routingKey,
        IReadOnlyBasicProperties original, byte[] body, int retryCount, string? error, CancellationToken ct)
    {
        var headers = original.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(original.Headers);

        headers[RetryHeader] = retryCount;
        if (error is not null) headers["x-last-error"] = error.Length > 500 ? error[..500] : error;

        var props = new BasicProperties
        {
            Persistent = true,
            MessageId = original.MessageId,
            CorrelationId = original.CorrelationId,
            ContentType = original.ContentType,
            Type = original.Type,
            Headers = headers
        };

        await channel.BasicPublishAsync(exchange, routingKey, mandatory: true, basicProperties: props, body: body, cancellationToken: ct);
    }

    private static int GetRetryCount(IReadOnlyBasicProperties props) =>
        props.Headers is not null && props.Headers.TryGetValue(RetryHeader, out var value)
            ? value switch
            {
                int i => i,
                long l => (int)l,
                byte[] b when int.TryParse(Encoding.UTF8.GetString(b), out var parsed) => parsed,
                _ => 0
            }
            : 0;

    private OrderCreated? Deserialize(byte[] body)
    {
        try { return JsonSerializer.Deserialize<OrderCreated>(body, JsonSerializerOptions.Web); }
        catch (JsonException ex) { logger.LogError(ex, "Invalid JSON body"); return null; }
    }

    private async Task<IConnection> ConnectWithRetryAsync(CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await connection.GetAsync(ct); }
            catch (Exception ex) when (attempt < 10 && ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                logger.LogWarning(ex, "RabbitMQ not reachable (attempt {Attempt}); retrying in {Delay}", attempt, delay);
                await Task.Delay(delay, ct);
            }
        }
    }
}