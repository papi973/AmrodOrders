using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Orders.API.Auth;
using Orders.API.Errors;
using Orders.API.Observability;
using Orders.API.OpenApi;
using Orders.API.Options;
using Orders.API.Validation;
using Orders.Infrastructure;
using Orders.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging: structured JSON with scopes (CorrelationId) 
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
    o.TimestampFormat = "O";
});

//Persistence 
var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing.");
builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<OrderingOptions>(builder.Configuration.GetSection("Ordering"));

// MVC, JSON, validation, errors 
builder.Services
    .AddControllers(o => o.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    });
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    if (ctx.HttpContext.GetCorrelationId() is { } id)
        ctx.ProblemDetails.Extensions["correlationId"] = id;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

//Auth 
builder.Services.AddOrdersAuth(builder.Configuration);

//  CORS for the React app 
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PUT")
    .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "If-Match", "If-None-Match", CorrelationIdMiddleware.HeaderName)
    .WithExposedHeaders("ETag", "Idempotent-Replayed", CorrelationIdMiddleware.HeaderName)));

// OpenAPI 
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// Health checks 
builder.Services.AddHealthChecks().AddDbContextCheck<OrdersDbContext>("sql", tags: ["ready"]);
//RabbitMQ check with tag "ready"

// Metrics: request duration histogram + counts via OpenTelemetry 
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Orders.API"))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()   // http.server.request.duration (count, sum, buckets)
        .AddPrometheusExporter());

var app = builder.Build();

//  Pipeline (order matters) 
app.UseMiddleware<CorrelationIdMiddleware>(); // first, so even exception logs carry the id
app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (ctx, next) =>                  // secure response headers
{
    var h = ctx.Response.Headers;
    h.XContentTypeOptions = "nosniff";
    h.XFrameOptions = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    if (ctx.Request.Path.StartsWithSegments("/api"))
        h.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Orders API v1"));
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous(); // liveness: process is up
app.MapHealthChecks("/readiness", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous(); // deps OK
app.MapPrometheusScrapingEndpoint().AllowAnonymous(); // /metrics

app.Run();

public partial class Program { }