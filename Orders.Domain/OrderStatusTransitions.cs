using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Orders.Domain
{
    public class OrderStatusTransitions
    {
        private static readonly Dictionary<OrderStatus, OrderStatus[]> UserAllowed = new()
        {
            [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
            [OrderStatus.Paid] = [OrderStatus.Fulfilled, OrderStatus.Cancelled],
            [OrderStatus.Fulfilled] = [],
            [OrderStatus.Cancelled] = [],
        };

        public static bool CanTransition(OrderStatus from, OrderStatus to, bool isSystem = false)
        {
            if (isSystem && from == OrderStatus.Pending && to == OrderStatus.Fulfilled)
                return true;

            return UserAllowed[from].Contains(to);
        }
    }
}