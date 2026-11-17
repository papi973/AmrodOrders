using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Orders.Domain;

namespace Orders.API.Contracts;

public sealed record CreateOrderLineRequest(string ProductSku, int Quantity, decimal UnitPrice);

public sealed record CreateOrderRequest(Guid CustomerId, string CurrencyCode, List<CreateOrderLineRequest> LineItems);

public sealed record UpdateOrderStatusRequest(OrderStatus? Status);

public sealed record OrderLineItemResponse(Guid Id, string ProductSku, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderResponse(
    Guid Id, Guid CustomerId, OrderStatus Status, string CurrencyCode,
    decimal TotalAmount, DateTime CreatedAt, List<OrderLineItemResponse> LineItems,
    string Version); // base64 rowversion; also sent as the ETag

public sealed record OrderSummaryResponse(
    Guid Id, Guid CustomerId, OrderStatus Status, string CurrencyCode, decimal TotalAmount, DateTime CreatedAt);

public sealed class OrderSearchQuery : PagingQuery
{
    public static readonly string[] AllowedSorts = ["createdAt", "-createdAt", "total", "-total"];

    [FromQuery(Name = "customerId")] public Guid? CustomerId { get; init; }
    [FromQuery(Name = "status")] public OrderStatus? Status { get; init; }
    [FromQuery(Name = "sort")] public string Sort { get; init; } = "-createdAt";
}

public static class OrderMappings
{
    // One SQL query: Orders LEFT JOIN OrderLineItems, no N+1.
    public static readonly Expression<Func<Order, OrderResponse>> Projection = o => new OrderResponse(
        o.Id, o.CustomerId, o.Status, o.CurrencyCode, o.TotalAmount, o.CreatedAt,
        o.LineItems
            .OrderBy(li => li.ProductSku)
            .Select(li => new OrderLineItemResponse(li.Id, li.ProductSku, li.Quantity, li.UnitPrice, li.Quantity * li.UnitPrice))
            .ToList(),
        Convert.ToBase64String(o.RowVersion));

    public static readonly Expression<Func<Order, OrderSummaryResponse>> SummaryProjection = o =>
        new OrderSummaryResponse(o.Id, o.CustomerId, o.Status, o.CurrencyCode, o.TotalAmount, o.CreatedAt);

    public static string ETag(this OrderResponse order) => $"\"{order.Version}\"";
}