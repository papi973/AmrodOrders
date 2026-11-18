using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Worker
{
    public sealed class WorkerOptions
    {
        public int AllocationDelaySeconds { get; set; } = 3;  // simulated downstream work
        public double FailureRate { get; set; }               // 0..1, to demo retries/DLQ
        public ushort PrefetchCount { get; set; } = 10;
    }
}
