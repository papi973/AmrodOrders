using System.Text.Json.Serialization;
using FluentValidation;
using Orders.API.Errors;
using Orders.API.Validation;
using Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing.");
builder.Services.AddInfrastructure(connectionString);

builder.Services
    .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 4xx responses (e.g. 404 from routing) also become Problem Details

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                  // /openapi/v1.json
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Orders API v1")); // /swagger
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { } // lets WebApplicationFactory find the entry point in integration tests