using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Domain
{
    public enum OrderStatus
    {
        Pending = 0,
        Paid = 1,
        Delivered = 2,
        Cancelled = 3
    }
}
