using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<OrdersDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly("Orders.API");
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            }));

            return services;
        }

        public static IServiceCollection AddMessaging(this IServiceCollection services, string rabbitMqUri)
        {
            services.AddSingleton(sp => new RabbitMqConnection(rabbitMqUri, sp.GetRequiredService<ILogger<RabbitMqConnection>>()));
            services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
            return services;
        }
    }
}
