using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain.Tests
{
    public class OrderTests
    {
        private static Customer SA_Customer() => new("Acme", "buyer@acme.co.za", "ZA");

        [Fact]
        public void Computes_total_server_side()
        {
            var order = Order.Create(SA_Customer(), "ZAR", [("SKU-1", 2, 10.50m), ("SKU-2", 3, 1.25m)]);

            Assert.Equal(24.75m, order.TotalAmount);
            Assert.Equal(OrderStatus.Pending, order.Status);
            Assert.Equal(2, order.LineItems.Count);
        }

        [Fact]
        public void Rejects_currency_not_allowed_for_country() =>
            Assert.Throws<DomainException>(() => Order.Create(SA_Customer(), "USD", [("SKU-1", 1, 1m)]));

        [Fact]
        public void Rejects_order_without_lines() =>
            Assert.Throws<DomainException>(() => Order.Create(SA_Customer(), "ZAR", []));

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Rejects_non_positive_quantity(int qty) =>
            Assert.Throws<DomainException>(() => Order.Create(SA_Customer(), "ZAR", [("SKU-1", qty, 1m)]));

        [Fact]
        public void Rejects_negative_price() =>
            Assert.Throws<DomainException>(() => Order.Create(SA_Customer(), "ZAR", [("SKU-1", 1, -0.01m)]));

        [Fact]
        public void Rejects_price_with_more_than_two_decimals() =>
            Assert.Throws<DomainException>(() => Order.Create(SA_Customer(), "ZAR", [("SKU-1", 1, 1.005m)]));

        [Fact]
        public void Allows_zero_price() =>
            Assert.Equal(0m, Order.Create(SA_Customer(), "ZAR", [("FREEBIE", 1, 0m)]).TotalAmount);

        [Fact]
        public void Rejects_non_sadc_customer() =>
            Assert.Throws<DomainException>(() => new Customer("X", "x@x.com", "US"));

        [Fact]
        public void Transition_to_same_status_is_a_noop()
        {
            var order = Order.Create(SA_Customer(), "ZAR", [("SKU-1", 1, 1m)]);
            Assert.False(order.TransitionTo(OrderStatus.Pending));
        }

        [Fact]
        public void Invalid_transition_throws()
        {
            var order = Order.Create(SA_Customer(), "ZAR", [("SKU-1", 1, 1m)]);
            order.TransitionTo(OrderStatus.Cancelled);
            Assert.Throws<DomainException>(() => order.TransitionTo(OrderStatus.Paid));
        }
    }
}

