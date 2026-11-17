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
            [OrderStatus.Paid] = [OrderStatus.Delivered, OrderStatus.Cancelled],
            [OrderStatus.Delivered] = [],
            [OrderStatus.Cancelled] = [],
        };

        public static bool CanTransition(OrderStatus from, OrderStatus to, bool isSystem = false)
        {
            if (isSystem && from == OrderStatus.Pending && to == OrderStatus.Delivered)
                return true;

            return UserAllowed[from].Contains(to);
        }
    }
}