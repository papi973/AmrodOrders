using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.API.Auth;
using Orders.API.Contracts;
using Orders.Domain;
using Orders.Infrastructure.Persistence;

namespace Orders.API.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class CustomersController(OrdersDbContext db) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Write)]
    public async Task<IActionResult> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Friendly check first; the unique index (UX_Customers_Email) still protects against races.
        if (await db.Customers.AnyAsync(c => c.Email == email, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict,
                           title: "Duplicate resource",
                           detail: $"A customer with email '{email}' already exists.");

        var customer = new Customer(request.Name, request.Email, request.CountryCode);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer.ToResponse());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthPolicies.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(CustomerMappings.Projection)
            .FirstOrDefaultAsync(ct);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<CustomerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = AuthPolicies.Read)]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> Search(
        [FromQuery] CustomerSearchQuery query, CancellationToken ct)
    {
        var customers = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            customers = customers.Where(c => c.Name.Contains(term) || c.Email.Contains(term));
        }

        var total = await customers.CountAsync(ct);

        var items = await customers
            .OrderBy(c => c.Name).ThenBy(c => c.Id) // deterministic order = stable paging
            .Skip(query.GetSkip())
            .Take(query.PageSize)
            .Select(CustomerMappings.Projection)
            .ToListAsync(ct);

        return new PagedResult<CustomerResponse>(items, query.Page, query.PageSize, total);
    }
}