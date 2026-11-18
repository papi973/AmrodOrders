using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Persistence
{
    public class ProcessedMessage
    {
        private ProcessedMessage() { }
        public Guid MessageId { get; private set; }
        public string Consumer { get; private set; } = null!;
        public DateTime ProcessedAt { get; private set; }

        public ProcessedMessage(Guid messageId, string consumer)
        {
            MessageId = messageId;
            Consumer = consumer;
            ProcessedAt = DateTime.UtcNow;
        }
    }
}
