using Orders.Infrastructure;
using Orders.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "O";
});

var sql = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing.");
var rabbitMq = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("Connection string 'RabbitMq' is missing.");

builder.Services.AddInfrastructure(sql);
builder.Services.AddMessaging(rabbitMq);
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddScoped<OrderAllocationProcessor>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

builder.Build().Run();