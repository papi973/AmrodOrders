using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Contracts
{
    public sealed record OrderCreated(Guid OrderId, Guid CustomerId, string CurrencyCode, decimal TotalAmount, DateTime CreatedAt);

    public static class MessageTypes
    {
        public const string OrderCreated = "Orders.Contracts.OrderCreated.v1";
    }
}
