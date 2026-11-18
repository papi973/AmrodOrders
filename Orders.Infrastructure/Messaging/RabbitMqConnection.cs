using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure.Messaging
{
    ///One long-lived connection per process (connections are expensive; channels are cheap).
    public sealed class RabbitMqConnection(string uri, ILogger<RabbitMqConnection> logger) : IDisposable
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private IConnection? _connection;

        public async Task<IConnection> GetAsync(CancellationToken ct)
        {
            if (_connection is { IsOpen: true }) return _connection;

            await _lock.WaitAsync(ct);
            try
            {
                if (_connection is { IsOpen: true }) return _connection;

                var factory = new ConnectionFactory
                {
                    Uri = new Uri(uri),                       // amqps:// (CloudAMQP) enables TLS automatically
                    ClientProvidedName = AppDomain.CurrentDomain.FriendlyName,
                    AutomaticRecoveryEnabled = true           // reconnect + re-declare after network blips
                };

                _connection = await factory.CreateConnectionAsync(ct);
                logger.LogInformation("Connected to RabbitMQ at {Host}", factory.HostName);
                return _connection;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose() => _connection?.Dispose();
    }
}
