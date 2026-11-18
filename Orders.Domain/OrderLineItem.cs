using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain
{
    public class OrderLineItem
    {
        private OrderLineItem() { }

        internal OrderLineItem(Guid orderId, string productSku, int quantity, decimal unitPrice)
        {
            if (string.IsNullOrWhiteSpace(productSku))
                throw new DomainException("line.sku_required", "ProductSku is required.");
            if (quantity <= 0)
                throw new DomainException("line.quantity_invalid", "Quantity must be greater than 0.");
            if (unitPrice < 0)
                throw new DomainException("line.price_negative", "UnitPrice cannot be negative.");
            if (decimal.Round(unitPrice, 2) != unitPrice)
                throw new DomainException("line.price_precision", "UnitPrice cannot have more than 2 decimal places.");

            Id = Guid.NewGuid();
            OrderId = orderId;
            ProductSku = productSku.Trim().ToUpperInvariant();
            Quantity = quantity;
            UnitPrice = unitPrice;
        }

        public Guid Id { get; private set; }
        public Guid OrderId { get; private set; }
        public string ProductSku { get; private set; } = null!;
        public int Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal LineTotal => Quantity * UnitPrice;

    }
}
