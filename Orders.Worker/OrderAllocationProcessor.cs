using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orders.Contracts;
using Orders.Domain;
using Orders.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Worker
{
    public class OrderAllocationProcessor(OrdersDbContext db, IOptions<WorkerOptions> options, ILogger<OrderAllocationProcessor> logger)
    {
        public const string ConsumerName = "OrderAllocationProcessor";

        public async Task ProcessAsync(Guid messageId, OrderCreated evt, CancellationToken ct)
        {
            // 1. Idempotency: at-least-once delivery means duplicates will happen.
            if (await db.ProcessedMessages.AnyAsync(p => p.MessageId == messageId && p.Consumer == ConsumerName, ct))
            {
                logger.LogInformation("Duplicate message ignored");
                return;
            }

            // 2. Simulate downstream allocation (warehouse call, stock reservation etc).
            logger.LogInformation("Allocating stock for order {OrderId}", evt.OrderId);
            await Task.Delay(TimeSpan.FromSeconds(options.Value.AllocationDelaySeconds), ct);

            if (options.Value.FailureRate > 0 && Random.Shared.NextDouble() < options.Value.FailureRate)
                throw new InvalidOperationException("Simulated allocation failure");

            // 3. Load after the delay so we work on fresh state (rowversion still guards races).
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == evt.OrderId, ct)
                ?? throw new InvalidOperationException($"Order {evt.OrderId} not found");

            if (order.Status is OrderStatus.Pending or OrderStatus.Paid)
            {
                order.TransitionTo(OrderStatus.Fulfilled, isSystem: true);
                logger.LogInformation("Order {OrderId} Fulfilled", order.Id);
            }
            else
            {
                logger.LogInformation("Order {OrderId} is {Status}; nothing to fulfil", order.Id, order.Status);
            }

            // 4. Status change + dedup record in ONE SaveChanges (atomic).
            db.ProcessedMessages.Add(new ProcessedMessage(messageId, ConsumerName));
            await db.SaveChangesAsync(ct);
        }

    }
}
