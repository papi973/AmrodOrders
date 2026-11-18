using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;

namespace Orders.API.Messsaging
{
    public sealed class OutboxDispatcher(IServiceScopeFactory scopes, IMessagePublisher publisher, ILogger<OutboxDispatcher> logger) : BackgroundService
    {
        private const int BatchSize = 20;
        private const int MaxAttempts = 10;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            do
            {
                try
                {
                    await DispatchBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Outbox dispatch cycle failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        public async Task DispatchBatchAsync(CancellationToken ct)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

            var batch = await db.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
                .OrderBy(m => m.OccurredAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) return;

            foreach (var message in batch)
            {
                using var _ = logger.BeginScope(new Dictionary<string, object?>
                {
                    ["CorrelationId"] = message.CorrelationId,
                    ["MessageId"] = message.Id
                });

                try
                {
                    await publisher.PublishAsync(new OutgoingMessage(
                        message.Id, message.Type, RabbitTopology.OrdersExchange,
                        message.RoutingKey, message.Payload, message.CorrelationId), ct);

                    message.MarkProcessed();
                    logger.LogInformation("Published {MessageType}", message.Type);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.MarkFailed(ex.Message);
                    logger.LogWarning(ex, "Publish failed (attempt {Attempts}); will retry next cycle", message.Attempts);
                    break; // broker likely down: stop this batch, keep ordering, try again in 2s
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
