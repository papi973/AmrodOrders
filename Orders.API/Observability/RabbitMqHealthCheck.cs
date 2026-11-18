using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orders.Infrastructure.Messaging;

namespace Orders.API.Observability;

public sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var conn = await connection.GetAsync(ct);
            return conn.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Connection closed");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ unreachable", ex);
        }
    }
}