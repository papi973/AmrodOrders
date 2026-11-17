using Microsoft.AspNetCore.Mvc;

namespace Orders.API.Contracts
{
    public class PagingQuery
    {
        public const int MaxPageSize = 100;
        [FromQuery(Name = "page")] public int Page { get; init; } = 1;
        [FromQuery(Name = "pageSize")] public int PageSize { get; init; } = 20;
        [FromQuery(Name = "search")] public string? Search { get; init; }

        public int GetSkip() => (Page - 1) * PageSize;
    }
    public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
    {
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
