using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Orders.Infrastructure.Persistence
{
    public class OutboxMessage
    {
        private OutboxMessage() { }
        public Guid Id { get; private set; }  //RabbitMQ message id
        public string Type { get; private set; } = null!;
        public string RoutingKey { get; private set; } = null!;
        public string Payload { get; private set; } = null!;
        public string? CorrelationId { get; private set; }
        public DateTime OccurredAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public int Attempts { get; private set; }
        public string? LastError { get; private set; }

        public static OutboxMessage Create<T>(string type, T message,string routingKey, string? correlationId = null)
        {
            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = type,
                RoutingKey = routingKey,
                Payload = System.Text.Json.JsonSerializer.Serialize(message, JsonSerializerOptions.Web),
                CorrelationId = correlationId,
                OccurredAt = DateTime.UtcNow,
                Attempts = 0
            };
        }

        public void MarkProcessed()
        {
            ProcessedAt = DateTime.UtcNow;
        }

        public void MarkFailed(string error)
        {
            Attempts++;
            LastError = error.Length > 2000 ? error[..2000] : error;
        }
    }
}
