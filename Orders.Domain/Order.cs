using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain
{
    public class Order
    {
        private readonly List<OrderLineItem> _lineItems = [];
        private Order() { }

        public Guid Id { get; private set; }
        public Guid CustomerId { get; private set; }
        public Customer? Customer { get; private set; }
        public OrderStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public string CurrencyCode { get; private set; } = null!;
        public decimal TotalAmount { get; private set; }
        public byte[] RowVersion { get; private set; } = [];
        public IReadOnlyCollection<OrderLineItem> LineItems => _lineItems.AsReadOnly();

        public static Order Create(
            Customer customer,
            string currencyCode,
            IEnumerable<(string Sku, int Quantity, decimal UnitPrice)> lines,
            bool allowRandInCma = false)
        {
            ArgumentNullException.ThrowIfNull(customer);

            var currency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!SADC_Countries.IsCurrencyAllowed(customer.CountryCode, currency, allowRandInCma))
                throw new DomainException("order.currency_not_allowed",
                    $"Currency '{currencyCode}' is not permitted for country '{customer.CountryCode}'.");

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                CurrencyCode = currency,
            };

            foreach (var (sku, qty, price) in lines)
                order._lineItems.Add(new OrderLineItem(order.Id, sku, qty, price));

            if (order._lineItems.Count == 0)
                throw new DomainException("order.no_lines", "An order must have at least one line item.");

            order.TotalAmount = decimal.Round(order._lineItems.Sum(li => li.LineTotal), 2, MidpointRounding.AwayFromZero);
            return order;
        }

        public bool TransitionTo(OrderStatus target, bool isSystem = false)
        {
            if (Status == target)
                return false;

            if (!OrderStatusTransitions.CanTransition(Status, target, isSystem))
                throw new DomainException("order.invalid_transition", $"Cannot move order from {Status} to {target}.");

            Status = target;
            return true;
        }
    }
}
