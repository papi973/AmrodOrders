using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.API.Auth;
using Orders.API.Contracts;
using Orders.API.IntegrationTests.Infrastructure;
using Orders.API.Messsaging;
using Orders.Contracts;
using Orders.Domain;
using Orders.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;

namespace Orders.API.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class OrdersApiTests(OrdersApiFactory factory)
{
    private readonly HttpClient _writer = factory.CreateClientWithRoles(AuthPolicies.Read, AuthPolicies.Write);

    // Create 

    [Fact]
    public async Task Create_order_computes_total_server_side_and_writes_outbox_event()
    {
        var customer = await CreateCustomerAsync("ZA");

        var order = await CreateOrderAsync(customer.Id, "ZAR",
            new CreateOrderLineRequest("PEN-BLUE", 2, 10.50m),
            new CreateOrderLineRequest("MUG-WHITE", 3, 1.25m));

        Assert.Equal(24.75m, order.TotalAmount);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.LineItems.Count);

        // OrderCreated was stored in the same transaction as the order.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var outbox = await db.OutboxMessages.AsNoTracking()
            .SingleAsync(m => m.Payload.Contains(order.Id.ToString()));

        Assert.Equal(MessageTypes.OrderCreated, outbox.Type);
        Assert.Null(outbox.ProcessedAt);
    }

    [Fact]
    public async Task Currency_not_allowed_for_country_returns_422()
    {
        var customer = await CreateCustomerAsync("ZA");

        var response = await _writer.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(customer.Id, "USD", [new("X", 1, 1m)]), TestJson.Options);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Outbox_dispatcher_publishes_OrderCreated_and_marks_it_processed()
    {
        var customer = await CreateCustomerAsync("BW");
        var order = await CreateOrderAsync(customer.Id, "BWP", new CreateOrderLineRequest("CAP-NAVY", 1, 99m));

        var dispatcher = new OutboxDispatcher(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Publisher,
            NullLogger<OutboxDispatcher>.Instance);

        // Other tests share the DB, so drain a few batches.
        for (var i = 0; i < 10; i++)
            await dispatcher.DispatchBatchAsync(CancellationToken.None);

        var published = Assert.Single(factory.Publisher.Published, m => m.Payload.Contains(order.Id.ToString()));
        Assert.Equal("order.created", published.RoutingKey);
        Assert.Equal(MessageTypes.OrderCreated, published.Type);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var outbox = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == published.MessageId);
        Assert.NotNull(outbox.ProcessedAt);
    }

    //  Read + caching 

    [Fact]
    public async Task Get_with_matching_ETag_returns_304()
    {
        var customer = await CreateCustomerAsync("ZA");
        var order = await CreateOrderAsync(customer.Id, "ZAR", new CreateOrderLineRequest("X", 1, 1m));

        var first = await _writer.GetAsync($"/api/orders/{order.Id}");
        var etag = first.Headers.ETag!.ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{order.Id}");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var second = await _writer.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    // Idempotent status updates 

    [Fact]
    public async Task Same_idempotency_key_is_applied_once_and_replayed()
    {
        var customer = await CreateCustomerAsync("ZA");
        var order = await CreateOrderAsync(customer.Id, "ZAR", new CreateOrderLineRequest("X", 1, 1m));
        var key = Guid.NewGuid().ToString();

        var first = await PutStatusAsync(order.Id, OrderStatus.Paid, key);
        var second = await PutStatusAsync(order.Id, OrderStatus.Paid, key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.False(first.Headers.Contains("Idempotent-Replayed"));
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        Assert.Equal(1, await db.IdempotencyRecords.CountAsync(r => r.Key == $"order-status:{key}"));
        Assert.Equal(OrderStatus.Paid, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);
    }

    [Fact]
    public async Task Same_key_with_different_body_returns_422()
    {
        var customer = await CreateCustomerAsync("ZA");
        var order = await CreateOrderAsync(customer.Id, "ZAR", new CreateOrderLineRequest("X", 1, 1m));
        var key = Guid.NewGuid().ToString();

        await PutStatusAsync(order.Id, OrderStatus.Paid, key);
        var reused = await PutStatusAsync(order.Id, OrderStatus.Cancelled, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var customer = await CreateCustomerAsync("ZA");
        var order = await CreateOrderAsync(customer.Id, "ZAR", new CreateOrderLineRequest("X", 1, 1m));

        var response = await PutStatusAsync(order.Id, OrderStatus.Paid, key: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_transition_returns_422()
    {
        var customer = await CreateCustomerAsync("ZA");
        var order = await CreateOrderAsync(customer.Id, "ZAR", new CreateOrderLineRequest("X", 1, 1m));

        // Users can't skip payment: Pending -> Fulfilled is worker-only.
        var response = await PutStatusAsync(order.Id, OrderStatus.Fulfilled, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Stale_If_Match_returns_412()
    {
        var customer = await CreateCustomerAsync("ZA");
        var order = await CreateOrderAsync(customer.Id, "ZAR", new CreateOrderLineRequest("X", 1, 1m));
        var staleEtag = $"\"{order.Version}\"";

        await PutStatusAsync(order.Id, OrderStatus.Paid, Guid.NewGuid().ToString());              // changes rowversion
        var response = await PutStatusAsync(order.Id, OrderStatus.Cancelled, Guid.NewGuid().ToString(), staleEtag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    // helpers 
    private async Task<CustomerResponse> CreateCustomerAsync(string country)
    {
        var response = await _writer.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest("Test Co", $"{Guid.NewGuid():N}@test.co", country), TestJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(TestJson.Options))!;
    }

    private async Task<OrderResponse> CreateOrderAsync(Guid customerId, string currency, params CreateOrderLineRequest[] lines)
    {
        var response = await _writer.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(customerId, currency, [.. lines]), TestJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderResponse>(TestJson.Options))!;
    }

    private Task<HttpResponseMessage> PutStatusAsync(Guid orderId, OrderStatus status, string? key, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{orderId}/status")
        {
            Content = JsonContent.Create(new UpdateOrderStatusRequest(status), options: TestJson.Options)
        };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return _writer.SendAsync(request);
    }
}