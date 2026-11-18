using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Orders.API.Messsaging;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;

namespace Orders.API.IntegrationTests.Infrastructure;

public sealed class OrdersApiFactory : WebApplicationFactory<Program>
{
    public OrdersApiFactory()
    {
        // Environment variables override appsettings and User Secrets, and Program.cs reads them eagerly.
        // CI sets TEST_SQL_CONNECTION to its SQL Server container.
        var sql = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION")
                  ?? "Server=localhost;Database=AmrodOrders_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True";

        Environment.SetEnvironmentVariable("ConnectionStrings__OrdersDb", sql);
        Environment.SetEnvironmentVariable("ConnectionStrings__RabbitMq", "amqp://guest:guest@localhost:5672/"); // never used
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Auth: swap JWT for the header-driven test scheme.
            services.AddAuthentication(TestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

            // Messaging: no background dispatcher, no real broker.
            var dispatcher = services.Single(d =>
                d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(OutboxDispatcher));
            services.Remove(dispatcher);

            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton<FakeMessagePublisher>();
            services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<FakeMessagePublisher>());
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Fresh database from migrations, once per test run.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        db.Database.EnsureDeleted();
        db.Database.Migrate();

        return host;
    }

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        var client = CreateClient();
        if (roles.Length > 0)
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
        return client;
    }

    public FakeMessagePublisher Publisher => Services.GetRequiredService<FakeMessagePublisher>();
}