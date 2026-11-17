using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orders.API.Contracts;
using Orders.API.Options;
using Orders.Domain;
using Orders.Infrastructure.Persistence;

namespace Orders.API.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController(OrdersDbContext db, IOptions<OrderingOptions> ordering) : ControllerBase
{
    private const string IdempotencyHeader = "Idempotency-Key";

    // POST /api/orders 
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);

        if (customer is null)
            return Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
                           title: "Customer not found",
                           detail: $"Customer '{request.CustomerId}' does not exist.");

        // Domain validates SADC currency pairing and computes TotalAmount server-side.
        var order = Order.Create(
            customer,
            request.CurrencyCode,
            request.LineItems.Select(l => (l.ProductSku, l.Quantity, l.UnitPrice)),
            ordering.Value.AllowRandInCma);

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        var response = (await LoadOrderAsync(order.Id, ct))!;
        Response.Headers.ETag = response.ETag();
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, response);
    }

    // GET /api/orders/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await LoadOrderAsync(id, ct);
        if (order is null) return NotFound();

        var etag = order.ETag();
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, no-cache"; // clients must revalidate

        if (Request.Headers.IfNoneMatch.ToString().Contains(etag))
            return StatusCode(StatusCodes.Status304NotModified);

        return Ok(order);
    }

    //  GET /api/orders?customerId=&status=&page=&pageSize=&sort= 
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<OrderSummaryResponse>>> Search(
        [FromQuery] OrderSearchQuery query, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking();

        if (query.CustomerId is { } customerId) orders = orders.Where(o => o.CustomerId == customerId);
        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);

        var total = await orders.CountAsync(ct);

        orders = query.Sort.ToLowerInvariant() switch
        {
            "createdat" => orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id),
            "total" => orders.OrderBy(o => o.TotalAmount).ThenBy(o => o.Id),
            "-total" => orders.OrderByDescending(o => o.TotalAmount).ThenBy(o => o.Id),
            _ => orders.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id),
        };

        var items = await orders
            .Skip(query.GetSkip())
            .Take(query.PageSize)
            .Select(OrderMappings.SummaryProjection)
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryResponse>(items, query.Page, query.PageSize, total);
    }

    //  PUT /api/orders/{id}/status 
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateOrderStatusRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                           title: "Missing or invalid Idempotency-Key header",
                           detail: "Send a unique Idempotency-Key (max 100 chars), e.g. a UUID, and reuse it when retrying.");

        var targetStatus = request.Status!.Value;
        var key = $"order-status:{idempotencyKey}";              // scope keys per operation
        var requestHash = Hash($"{id}:{targetStatus}");

        // 1. Replay: same key seen before?
        var previous = await db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key, ct);

        if (previous is not null)
        {
            if (previous.RequestHash != requestHash)
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
                               title: "Idempotency-Key reuse",
                               detail: "This Idempotency-Key was already used with a different request.");

            Response.Headers["Idempotent-Replayed"] = "true";
            return await OrderResult(id, ct);
        }

        // 2. Load the order (tracked) and check If-Match if the client sent one.
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();

        var currentEtag = $"\"{Convert.ToBase64String(order.RowVersion)}\"";
        if (Request.Headers.IfMatch.Count > 0 && Request.Headers.IfMatch.ToString() != currentEtag)
            return Problem(statusCode: StatusCodes.Status412PreconditionFailed,
                           title: "Precondition failed",
                           detail: "The order has changed since you last read it. GET it again and retry.");

        // 3. Apply the transition (throws DomainException -> 422 if invalid; same status = no-op).
        order.TransitionTo(targetStatus);

        // 4. Save the status change and the idempotency record atomically (one SaveChanges = one transaction).
        db.IdempotencyRecords.Add(new IdempotencyRecord(key, requestHash, id, StatusCodes.Status200OK));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else (another request or the worker) changed the order between our read and write.
            var current = await LoadOrderAsync(id, ct);
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Concurrency conflict",
                Detail = "The order was updated by another process. Review the current state and retry.",
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["currentState"] = current;
            return Conflict(problem);
        }

        return await OrderResult(id, ct);
    }

    // helpers
    private Task<OrderResponse?> LoadOrderAsync(Guid id, CancellationToken ct) =>
        db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(OrderMappings.Projection)
            .FirstOrDefaultAsync(ct);

    private async Task<IActionResult> OrderResult(Guid id, CancellationToken ct)
    {
        var order = await LoadOrderAsync(id, ct);
        if (order is null) return NotFound();
        Response.Headers.ETag = order.ETag();
        return Ok(order);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}