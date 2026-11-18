Async/await in ASP.NET Core; when to use Task.Run

Takeaway: async is about freeing threads, not making one request faster.

I use async/await for all I/O (database, HTTP, RabbitMQ). While a request waits, its thread goes back to the pool and serves other requests. The rules I follow: async all the way, never .Result or .Wait() (deadlocks, thread starvation), always pass the CancellationToken, and no async void.

I don't use Task.Run in a web API. It only moves work to another pool thread. Background work belongs in a hosted service.

In this project: every controller action and EF query takes the request's CancellationToken. The outbox dispatcher and the Worker are BackgroundServices.

2. Minimal APIs vs controllers

Takeaway: Minimal APIs for small services, controllers for structured APIs.

Minimal APIs are lightweight and fast, which suits small or focused services. Controllers give conventions, filters and model binding out of the box, which suits larger APIs with shared cross-cutting behaviour.

In this project: I used controllers with a global FluentValidation filter. One gotcha I hit: a class-level [Produces("application/json")] overrode the application/problem+json content type on error responses. An integration test caught it.

3. Tracking vs AsNoTracking(); optimistic concurrency

Takeaway: track only what you'll save; let rowversion detect lost updates.

Tracking costs memory and CPU, so reads use AsNoTracking() and project straight to DTOs with Select. For concurrency, a rowversion column makes EF add WHERE RowVersion = @original to every update. If no row matches, EF throws DbUpdateConcurrencyException.

In this project: the API returns 409 with the order's current state. The version doubles as the ETag, so a stale If-Match returns 412 before any write happens.

4. Where business rules live; FluentValidation vs manual

Takeaway: validators check shape, the domain enforces rules, the database is the safety net.

DTO validation (FluentValidation) rejects badly formed requests → 400.
Domain rules, such as the SADC currency pairing and status transitions, live in the entities → 422. They apply no matter who calls them.
Database constraints catch whatever slips through, for example two duplicate inserts racing each other.

I prefer FluentValidation because it's declarative, testable and keeps controllers thin.

In this project: the Worker never goes through HTTP but uses the same Order.TransitionTo rules. The unique email index catches the duplicate race that the AnyAsync pre-check can't.

5. Global exception handling and Problem Details

Takeaway: one handler, one error format (RFC 7807), no leaks.

AddProblemDetails() plus an IExceptionHandler map exceptions to status codes in one place. Every error has the same shape: type, title, status, detail, plus traceId, correlationId, a machine-readable code, and errors for validation failures. The content type is application/problem+json. On 500s, the client gets a generic message, and the details go to the logs.

In this project: DomainException → 422 with a code; concurrency conflict → 409; duplicate key (SQL 2601/2627) → 409.

6. REST search design; when GraphQL; N+1

Takeaway: filter, sort, page, and always cap pageSize.

Search endpoints take filters as query parameters, page/pageSize with a max of 100, and a whitelisted sort with a stable tiebreaker. They return totalCount and totalPages. GraphQL is worth it when clients need very different data shapes, but it makes caching and authorisation harder. N+1 means one query for the list plus one query per row. I avoid it with projections or Include.

In this project: GET /api/orders?customerId=&status=&sort=-total&page=&pageSize=. Order details, including line items, load in a single query.

7. Entra: JWT validation, roles, token lifetimes, policies

Takeaway: validate the token, map roles to policies, deny by default.

Microsoft.Identity.Web validates the token's signature, issuer, audience and lifetime. App roles defined in the app registration arrive in the roles claim, and named policies check them. Access tokens are short-lived (about an hour), and the client refreshes them through MSAL. A fallback policy means any new endpoint requires authentication by default.

In this project: policies Orders.Read / Orders.Write / Orders.Admin, where Admin includes Write and Write includes Read. There's a dev mode using dotnet user-jwts, and tests cover 401 and 403.

8. RabbitMQ: exchanges, durability, at-least-once, idempotency

Takeaway: durable + persistent + confirms; expect duplicates, make the consumer idempotent.

The exchange types are direct, topic, fanout and headers. Messages survive a broker restart only if the exchange is durable, the queue is durable, and the messages are persistent. Publisher confirms make sure the broker actually received them. Manual acks give at-least-once delivery, which means duplicates will happen. So the consumer records each message id in the same transaction as its effect, and skips repeats.

In this project:

- A transactional outbox guarantees the event is sent;
- ProcessedMessages deduplicates in the Worker;
- Retry queues back off at 5 s, 30 s and 2 min, and then the message goes to a DLQ.

9. Frontend state management; type-safe contracts

Takeaway: server state goes in a query cache; types are generated from the API.

Server data belongs in TanStack Query (or RTK Query/SWR), which handles caching, refetching and invalidation. Redux is for complex client-only state, and Context for small global values such as the theme. For type safety, I generate TypeScript types from the API's OpenAPI document, so a C# contract change breaks the frontend build instead of production.

In this project: 
 - TanStack Query hooks
 - Filters stored in the URL
 - The order page polls while Pending (cheap, because of ETag 304s) 
 - npm run gen:api regenerates the types.

10. Test pyramid and CI gates

Takeaway: many unit tests, some integration tests, few E2E; CI blocks anything that fails.

Unit tests cover business rules and run in milliseconds. Integration tests cover HTTP, validation, auth and a real SQL Server built from the migrations (an in-memory database hides real SQL behaviour). E2E tests cover only the critical user journeys. CI gates: build, lint, typecheck, tests with coverage, dependency vulnerability scan, migration script check and Docker build.

In this project, CI caught real problems: a .gitignore rule hiding frontend files, sqlcmd needing -I for a filtered index, and the Problem Details content-type bug.

11. Performance, scalability, observability

Takeaway: accept fast, process async, index for real queries, trace everything.

For write spikes, I accept the request quickly and do the heavy work asynchronously through a queue, with prefetch limits as backpressure and workers that scale horizontally. Indexes are designed for the actual queries (covering, so there are no key lookups). A correlation id follows each request through every service. Example SLOs: 99% of GET /api/orders/{id} under 200 ms, and 95% of orders fulfilled within 60 s, with alerts on DLQ depth.

In this project:

- The outbox and Worker absorb spikes;
- One correlation id follows a request from the browser to the Worker;
- /metrics exposes request duration;
- Readiness reports Degraded rather than Unhealthy when RabbitMQ is down, because orders are still accepted.

====================================================================================
SQL section

12. Pagination query + separate COUNT

- To get a customer id: SELECT Id FROM Customers WHERE Email = '<email>' (or GET /api/customers?search=...).

```sql
DECLARE @CustomerId uniqueidentifier = '<customer id>', @Offset int = 0, @PageSize int = 20;

SELECT Id, Status, CreatedAt, TotalAmount
FROM Orders
WHERE CustomerId = @CustomerId
ORDER BY CreatedAt DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(*) AS TotalCount FROM Orders WHERE CustomerId = @CustomerId;
```

-Parameters (not string concatenation) give plan reuse and prevent SQL injection. Deep OFFSET pages get slow; keyset pagination is the alternative.

13. Top 10 spenders (last 90 days, including customers with zero orders)
```sql
SELECT TOP 10 c.Id, c.Name,
       COALESCE(SUM(o.TotalAmount), 0) AS TotalSpend,
       COUNT(o.Id) AS OrderCount
FROM Customers c
LEFT JOIN Orders o
  ON o.CustomerId = c.Id
 AND o.CreatedAt >= DATEADD(DAY, -90, SYSUTCDATETIME())
GROUP BY c.Id, c.Name
ORDER BY TotalSpend DESC;
```

- The date filter goes in the ON clause. In WHERE, it would turn the LEFT JOIN into an inner join, and customers without orders would disappear.
COUNT(o.Id) returns 0 for those customers.
- I assume a single currency here. Otherwise I'd group by CurrencyCode, because summing across currencies is meaningless.


14. Covering index vs clustered index choice
```sql
CREATE INDEX IX_Orders_CustomerId_Status_CreatedAt
ON Orders (CustomerId, Status, CreatedAt DESC)
INCLUDE (TotalAmount, CurrencyCode)
```

- Equality columns come first, then the sort column.
- INCLUDE makes the index covering for list queries, so there are no key lookups. The trade-off is extra storage and write cost.
- The clustered key is a random GUID, which fragments on insert. Better options are sequential GUIDs, or clustering on (CreatedAt, Id), which also suits partitioning (Q19).

15. Detecting and removing key lookups

```sql
SELECT o.Id, o.CreatedAt, li.ProductSku, li.Quantity, li.UnitPrice
FROM Orders o
JOIN OrderLineItems li ON li.OrderId = o.Id
WHERE o.CustomerId = '<customer id>' AND o.Status = 'Fulfilled'
```

--fix: cover the line-item columns
CREATE INDEX IX_OrderLineItems_OrderId_Covering
ON OrderLineItems (OrderId) INCLUDE (ProductSku, Quantity, UnitPrice);

Detect: in the actual execution plan (Ctrl+M), look for a Key Lookup (Clustered) operator next to an Index Seek. Its Output List shows the missing columns. Fix: add those columns to the index's INCLUDE, then compare the plans before and after.

16. Optimistic concurrency with rowversion
```sql
ALTER TABLE Orders ADD RowVersion rowversion;
```

```csharp
b.Property(o => o.RowVersion).IsRowVersion();


try
{
    await db.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException)
{
    return Conflict("Order was updated by another process");
}
```

- SQL Server changes rowversion on every update. EF checks it, and 0 rows updated means someone else changed the row first. In the project, the 409 includes the current state so the client can re-read and decide. The same value is the ETag, so If-Match rejects stale writes up front (412).

17. Reader/writer deadlock on Orders + mitigation

Scenario: a user loads the orders list (GET /api/orders?customerId=...) at the same moment the Worker updates an order to Fulfilled.

The reader finds the order through IX_Orders_CustomerId_Status_CreatedAt, locks that index entry, then jumps to the table row to fetch the remaining columns (a key lookup).
The Worker has already locked that table row. Because Status is in the index, it also needs the index entry the reader holds.
Each waits for the other, so SQL Server cancels one of them (error 1205).

Mitigation:

- Covering index: it includes TotalAmount and CurrencyCode, so the list query never touches the table row. No key lookup means no lock cycle.
- Short transactions: each update is a single SaveChanges.
- Retry: EF's EnableRetryOnFailure retries a deadlock victim.
- Optionally, ALTER DATABASE AmrodOrders SET READ_COMMITTED_SNAPSHOT ON; so readers don't take shared locks at all.

18. Running total per customer

```sql
SELECT CustomerId, Id, CreatedAt, TotalAmount,
       SUM(TotalAmount) OVER (PARTITION BY CustomerId ORDER BY CreatedAt, Id
                              ROWS UNBOUNDED PRECEDING) AS RunningTotal
FROM Orders
ORDER BY CustomerId, CreatedAt, Id
```

- The explicit ROWS frame is faster than the default RANGE, and it treats ties correctly. Id makes the order deterministic.

19. Partitioning strategy + archival switching

```sql
-- Monthly partitions on CreatedAt
CREATE PARTITION FUNCTION pf_OrdersByMonth (datetime2)
AS RANGE RIGHT FOR VALUES ('2026-01-01', '2026-02-01', '2026-03-01' /* one per month, created ahead */);

CREATE PARTITION SCHEME ps_OrdersByMonth
AS PARTITION pf_OrdersByMonth ALL TO ([PRIMARY]);

-- Archive the oldest month: metadata-only, instant
ALTER TABLE Orders SWITCH PARTITION 2 TO Orders_Archive PARTITION 2;

-- Sliding window: add next month, remove the emptied boundary
ALTER PARTITION SCHEME ps_OrdersByMonth NEXT USED [PRIMARY];
ALTER PARTITION FUNCTION pf_OrdersByMonth() SPLIT RANGE ('2026-12-01');
ALTER PARTITION FUNCTION pf_OrdersByMonth() MERGE RANGE ('2026-01-01');

- Monthly partitions match how orders are queried and retained (recent orders are hot). Archiving becomes a switch instead of a huge DELETE.
- The clustered index must include CreatedAt, and all indexes must be aligned to the scheme.
- Queries benefit only when they filter on CreatedAt.
- Caveat: a table referenced by a foreign key (OrderLineItems → Orders) can't be switched out. Line items would need to be partitioned the same way, or the FK handled during archival.
```

20. sp_GetTransactionReport

```sql
CREATE OR ALTER PROCEDURE sp_GetTransactionReport
    @From datetime2,
    @To datetime2,
    @Status varchar(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- details
    SELECT o.Id, o.CreatedAt, o.Status, o.CurrencyCode, o.TotalAmount,
           c.Name AS Customer, li.ProductSku, li.Quantity, li.UnitPrice
    FROM Orders o
    JOIN Customers c ON c.Id = o.CustomerId
    JOIN OrderLineItems li ON li.OrderId = o.Id
    WHERE o.CreatedAt >= @From AND o.CreatedAt < @To
      AND (@Status IS NULL OR o.Status = @Status);

    -- summary (per currency)
    SELECT CurrencyCode, COUNT(*) AS TotalOrders, SUM(TotalAmount) AS GrandTotalAmount
    FROM Orders
    WHERE CreatedAt >= @From AND CreatedAt < @To
      AND (@Status IS NULL OR Status = @Status)
    GROUP BY CurrencyCode;
END;
GO

EXEC sp_GetTransactionReport '2026-11-01', '2026-12-01'
```

- It returns two result sets: details, then a summary.
- @Status is optional (NULL means all statuses).
- The half-open date range (>= @From AND < @To) avoids midnight edge cases.
- The summary is grouped per currency because CMA currencies are at par but are still different currencies.