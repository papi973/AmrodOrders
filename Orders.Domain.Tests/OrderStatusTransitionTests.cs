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
        [InlineData(Pending, Fulfilled, false)]   // users can't skip payment
        [InlineData(Paid, Fulfilled, true)]
        [InlineData(Paid, Cancelled, true)]
        [InlineData(Paid, Pending, false)]
        [InlineData(Fulfilled, Cancelled, false)]
        [InlineData(Fulfilled, Pending, false)]
        [InlineData(Cancelled, Paid, false)]
        public void User_transitions(OrderStatus from, OrderStatus to, bool expected) =>
       Assert.Equal(expected, OrderStatusTransitions.CanTransition(from, to));

        [Fact]
        public void Worker_may_fulfil_pending_order() =>
            Assert.True(OrderStatusTransitions.CanTransition(Pending, Fulfilled, isSystem: true));

        [Fact]
        public void Worker_cannot_fulfil_cancelled_order() =>
            Assert.False(OrderStatusTransitions.CanTransition(Cancelled, Fulfilled, isSystem: true));
    }
}

