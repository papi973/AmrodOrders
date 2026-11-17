using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Persistence
{
    public class IdempotencyRecord
    {
        private IdempotencyRecord() { }

        public IdempotencyRecord(string key, string requestHash, Guid resourceId, int statusCode)
        {
            Key = key;
            RequestHash = requestHash;
            ResourceId = resourceId;
            StatusCode = statusCode;
            CreatedAt = DateTime.UtcNow;
        }

        public string Key { get; private set; } = null!;
        public string RequestHash { get; private set; } = null!;
        public Guid ResourceId { get; private set; }
        public int StatusCode { get; private set; }
        public DateTime CreatedAt { get; private set; }
    }
}
