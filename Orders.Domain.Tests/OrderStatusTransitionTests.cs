using System;
using System.Collections.Generic;
using System.Text;
using static Orders.Domain.OrderStatus;

namespace Orders.Domain.Tests
{
    public class OrderStatusTransitionTests
    {
        [Theory]
        [InlineData(Pending, Paid, true)]
        [InlineData(Pending, Cancelled, true)]
        [InlineData(Pending, Delivered, false)]   // users can't skip payment
        [InlineData(Paid, Delivered, true)]
        [InlineData(Paid, Cancelled, true)]
        [InlineData(Paid, Pending, false)]
        [InlineData(Delivered, Cancelled, false)]
        [InlineData(Delivered, Pending, false)]
        [InlineData(Cancelled, Paid, false)]
        public void User_transitions(OrderStatus from, OrderStatus to, bool expected) =>
       Assert.Equal(expected, OrderStatusTransitions.CanTransition(from, to));

        [Fact]
        public void Worker_may_fulfil_pending_order() =>
            Assert.True(OrderStatusTransitions.CanTransition(Pending, Delivered, isSystem: true));

        [Fact]
        public void Worker_cannot_fulfil_cancelled_order() =>
            Assert.False(OrderStatusTransitions.CanTransition(Cancelled, Delivered, isSystem: true));
    }
}

