using Microsoft.AspNetCore.Mvc;
using Orders.Domain;
using System.Linq.Expressions;

namespace Orders.API.Contracts;

public sealed record CreateCustomerRequest(string Name, string Email, string CountryCode);

public sealed record CustomerResponse(Guid Id, string Name, string Email, string CountryCode, DateTime CreatedAt);

public sealed class CustomerSearchQuery : PagingQuery
{
    [FromQuery(Name = "search")] public string? Search { get; init; }
}

public static class CustomerMappings
{
    public static readonly Expression<Func<Customer, CustomerResponse>> Projection =
        c => new CustomerResponse(c.Id, c.Name, c.Email, c.CountryCode, c.CreatedAt);

    public static CustomerResponse ToResponse(this Customer c) =>
        new(c.Id, c.Name, c.Email, c.CountryCode, c.CreatedAt);
}