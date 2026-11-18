# Amrod Orders - SADC Order Management

A small full-stack order management system for customers in the Southern African Development Community (SADC).

- **API:** ASP.NET Core (.NET 10), EF Core code-first, SQL Server
- **Worker:** .NET background service consuming RabbitMQ
- **Web:** React + TypeScript (Vite), TanStack Query, typed client generated from OpenAPI
- **Messaging:** RabbitMQ with a transactional outbox, retry with backoff and a dead-letter queue
- **Auth:** Microsoft Entra ID via Microsoft.Identity.Web, with a dev/mock JWT mode
- **CI:** GitHub Actions (build → lint → security scan → tests + coverage → migration script verification → Docker build)

> **.NET version:** the brief specifies .NET 8. This solution targets **.NET 10**, the current LTS release. .NET 8 reached end of support in November 2026, so starting a new service on it would not be a sensible production choice.

---

## Contents

1. [Quick start (Docker)](#quick-start-docker)
2. [Running locally (development)](#running-locally-development)
3. [Architecture](#architecture)
4. [Key design decisions](#key-design-decisions)
5. [Database: migrations and lifecycle](#database-migrations-and-lifecycle)
6. [Messaging](#messaging)
7. [Security](#security)
8. [Observability](#observability)
9. [Testing](#testing)
10. [CI/CD](#cicd)
11. [SADC currencies and the Common Monetary Area](#sadc-currencies-and-the-common-monetary-area)
12. [Trade-offs and what I'd do with more time](#trade-offs-and-what-id-do-with-more-time)

---

## Quick start (Docker)

Requires Docker Desktop.

```bash
cp .env.example .env          # PowerShell: Copy-Item .env.example .env
docker compose up --build
```

| Service | URL |
|---|---|
| Web UI | http://localhost:8081 |
| API + Swagger | http://localhost:8080/swagger (click **Authorize** and paste `DEV_JWT_TOKEN` from `.env`) |
| RabbitMQ management | http://localhost:15672 (guest / guest) |
| SQL Server (SSMS) | `localhost,14333`, login `sa`, password `MSSQL_SA_PASSWORD` from `.env` |
| Health | http://localhost:8080/healthz, http://localhost:8080/readiness |
| Metrics (Prometheus) | http://localhost:8080/metrics |

Compose starts: `sqlserver` → `migrator` (applies EF migrations, then exits) → `api`, `worker` → `web` (nginx, proxies `/api` to the API).

> `.env.example` contains **demo-only** values: an SA password, a JWT signing key and a pre-signed token (roles `Orders.Read` + `Orders.Write`, expires 2028). They only work against this local Compose configuration.

**Try it:** create a customer → create an order → the order shows **Pending**, and a few seconds later the worker fulfils it (the page polls and updates to **Fulfilled**).

Screenshots: 
![Order details](Docs/OrderDetails.png)
![RabbitMQ Example](Docs/RabbitMQFulfillment.png)

---

## Running locally (development)

**Prerequisites:** .NET 10 SDK, Node 24, SQL Server (local instance), a RabbitMQ broker (local, Docker, or CloudAMQP).

### 1. Configuration (User Secrets: nothing secret is committed)

```bash
dotnet user-secrets set "ConnectionStrings:OrdersDb" "Server=localhost;Database=AmrodOrders;Trusted_Connection=True;TrustServerCertificate=True" --project Orders.API
dotnet user-secrets set "ConnectionStrings:RabbitMq" "amqp://guest:guest@localhost:5672/" --project Orders.API
# same two values for the worker
dotnet user-secrets set "ConnectionStrings:OrdersDb" "..." --project Orders.Worker
dotnet user-secrets set "ConnectionStrings:RabbitMq" "..." --project Orders.Worker
```

### 2. Database

```bash
dotnet tool restore
dotnet ef database update --project Orders.API --startup-project Orders.API
```

### 3. Dev tokens (mock Entra)

```bash
dotnet user-jwts create --project Orders.API --name dev-writer --role Orders.Read --role Orders.Write
dotnet user-jwts create --project Orders.API --name dev-reader --role Orders.Read
```

Use the printed token in `Orders.API/Orders.API.http` (`@token = ...`) and in `web/.env.development.local`:

```
VITE_DEV_TOKEN=<writer token>
```

### 4. Run

- **API + Worker:** set both as startup projects in Visual Studio (or `dotnet run` each). API: `https://localhost:7284/swagger`.
- **Web:**
  ```bash
  cd web
  npm install
  npm run dev        # http://localhost:5173 (proxies /api to the API)
  ```

Example requests for every endpoint, including error cases, are in [`Orders.API/Orders.API.http`](Orders.API/Orders.API.http).

---

## Architecture

```mermaid
flowchart LR
    UI[React UI]

    subgraph APIBOX [Orders.API]
        C[Controllers]
        D[Outbox Dispatcher]
    end

    DB[(SQL Server)]

    subgraph MQBOX [RabbitMQ]
        X{{orders.events exchange}}
        Q[orders.order-created queue]
        R[Retry queues 5s 30s 2m]
        DLQ[Dead letter queue]
    end

    W[Orders.Worker]

    UI -->|HTTP with JWT| C
    C -->|Order and outbox event in one transaction| DB
    D -->|Read unsent events| DB
    D -->|Publish with confirms| X
    X -->|order.created| Q
    Q -->|Consume| W
    W -->|Set Fulfilled and dedup record| DB
    W -.->|On failure| R
    R -.->|After TTL| X
    W -.->|After 3 retries| DLQ
```

| Project | Responsibility | Depends on |
|---|---|---|
| `Orders.Domain` | Entities, status state machine, SADC rules, totals. **No framework dependencies.** | – |
| `Orders.Contracts` | Integration events (`OrderCreated` v1) shared by API and Worker | – |
| `Orders.Infrastructure` | `OrdersDbContext`, EF configurations, outbox, RabbitMQ connection/publisher/topology | Domain, Contracts |
| `Orders.API` | REST endpoints, validation, auth, Problem Details, outbox dispatcher, **EF migrations** | all of the above |
| `Orders.Worker` | `OrderCreated` consumer: idempotent allocation → Fulfilled | Domain, Contracts, Infrastructure |
| `Orders.Domain.Tests` | Unit tests for business rules | Domain |
| `Orders.API.IntegrationTests` | WebApplicationFactory tests against real SQL Server | API |
| `web/` | React + TS frontend | OpenAPI document |

---

## Key design decisions

### Order status state machine

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Pending
    Pending --> Paid: user
    Pending --> Cancelled: user
    Pending --> Fulfilled: worker only
    Paid --> Fulfilled: user or worker
    Paid --> Cancelled: user
    Fulfilled --> [*]
    Cancelled --> [*]
```

The brief lists `Pending → Paid → Fulfilled → Cancelled`, but also says the worker fulfils a newly created (Pending) order. I resolved this as follows:

- **User transitions (API):** Pending → Paid | Cancelled; Paid → Fulfilled | Cancelled. Fulfilled and Cancelled are terminal.
- **System transition (Worker only):** Pending → Fulfilled, after simulated allocation. The worker also fulfils Paid orders, and skips orders that are already Cancelled or Fulfilled.
- Setting an order to the status it already has is a no-op (200), which keeps the operation idempotent by nature.

The rules live in `Orders.Domain/OrderStatusTransitions.cs`, so the API and Worker share one implementation.

### Where validation lives

| Layer | Checks | Result |
|---|---|---|
| FluentValidation (DTO) | Shape: required fields, lengths, `pageSize ≤ 100`, allowed sort values, 2-dp prices | **400** `ValidationProblemDetails` |
| Domain | Business rules: SADC country/currency pairing, quantity > 0, transitions, totals | **422** Problem Details with a `code` (e.g. `order.invalid_transition`) |
| Database | Last line of defence: check constraints, unique email, FKs | Mapped to **409** where relevant |

### API conventions

- **Errors:** RFC 7807 Problem Details (`application/problem+json`) with `traceId` and `correlationId`. No internal details on 500s.
- **Pagination:** `page`, `pageSize` (max 100) → `{ items, page, pageSize, totalCount, totalPages }`. Sorting is whitelisted (`createdAt`, `-createdAt`, `total`, `-total`) with a stable tiebreaker on `Id`.
- **Reads:** `AsNoTracking()` plus projections to DTOs, so each endpoint is one SQL query (no N+1).
- **Caching:** `GET /api/orders/{id}` returns an **ETag** (from `rowversion`). `If-None-Match` → **304**.
- **Idempotent status update:** `PUT /api/orders/{id}/status` requires an `Idempotency-Key` header.
  - Keys are stored in `IdempotencyRecords` together with a hash of the request, in the **same SaveChanges** as the status change.
  - Same key + same request → replayed (200, `Idempotent-Replayed: true`, nothing re-applied).
  - Same key + different request → 422.
  - The replay returns the order's **current** representation, not a stored copy of the original response. This keeps the write atomic, and the current state of the order is what the caller needs.
  - Failed attempts (e.g. 422) are not recorded, so the client can retry with the same key.
- **Concurrency:** `rowversion` on Orders.
  - Optional `If-Match` → **412** if the order changed since it was read.
  - A lost race between two writers (e.g. API and Worker) → `DbUpdateConcurrencyException` → **409**, including the current state.

---

## Database: migrations and lifecycle

Migrations live in [`Orders.API/Migrations`](Orders.API/Migrations). The `DbContext` lives in Infrastructure and uses `MigrationsAssembly("Orders.API")`.

| Migration | Purpose |
|---|---|
| `InitialCreate` | Tables, FKs (Customer → Orders **Restrict**; Order → LineItems **Cascade**), `decimal(18,2)`, check constraints, unique email, `IX_Orders_CustomerId_Status_CreatedAt` (covering: `INCLUDE (TotalAmount, CurrencyCode)`) |
| `AddOrderRowVersion` | `rowversion` on Orders for optimistic concurrency |
| `AddIdempotencyRecords` | Idempotency-Key storage for status updates |
| `AddOutboxAndProcessedMessages` | Transactional outbox (with a filtered index on unprocessed rows) and consumer dedup table |
| `RestoreFulfilledOrderStatus` | Corrects a status rename: relaxes the check constraint, **backfills data**, re-applies the constraint. `Down()` reverses it |

### Commands

```bash
dotnet tool restore                                        # restores dotnet-ef from .config/dotnet-tools.json

# create / apply
dotnet ef migrations add <Name> --project Orders.API --startup-project Orders.API --output-dir Migrations
dotnet ef database update --project Orders.API --startup-project Orders.API
dotnet ef migrations list --project Orders.API --startup-project Orders.API

# roll back to a specific migration (runs Down() of everything after it)
dotnet ef database update AddOrderRowVersion --project Orders.API --startup-project Orders.API

# remove the last migration (only if not yet applied)
dotnet ef migrations remove --project Orders.API --startup-project Orders.API

# SQL scripts (for review / DBA-run deployments)
dotnet ef migrations script --idempotent --project Orders.API --startup-project Orders.API --output db/scripts/migrate-idempotent.sql
dotnet ef migrations script InitialCreate AddOrderRowVersion --project Orders.API --startup-project Orders.API --output db/scripts/002_AddOrderRowVersion.up.sql
dotnet ef migrations script AddOrderRowVersion InitialCreate --project Orders.API --startup-project Orders.API --output db/scripts/002_AddOrderRowVersion.down.sql
```

Generated scripts are in [`db/scripts`](db/scripts).

> **Running `migrate-idempotent.sql` with sqlcmd:** the script creates a filtered index, which requires `QUOTED_IDENTIFIER ON`. SSMS and EF Core set this by default, but **sqlcmd does not**, so pass `-I`:
> `sqlcmd -S <server> -d AmrodOrders -I -i db/scripts/migrate-idempotent.sql`
> CI found this. It verifies the script exactly this way.

### Automation

- **CI:** generates the idempotent script and runs it **twice** with `sqlcmd` against an empty SQL Server container. The first run proves it builds the schema from scratch; the second proves it is idempotent.
- **Docker Compose:** a one-shot `migrator` container applies migrations. `api` and `worker` only start once it exits successfully (`service_completed_successfully`).
- **Production (proposed):** a migration **bundle** (`dotnet ef migrations bundle`) or the reviewed idempotent script, run as a release pipeline step before the app deploys. Apps should never migrate on startup in production, because several instances would race each other.

### Zero-downtime migration strategy

Use **expand → migrate → contract**, so that the running version and the next version of the app can both work with the schema at every point.

1. **Expand.** Make additive changes only: new nullable columns (or columns with defaults), new tables, new indexes (`ONLINE = ON` where the edition supports it). The running app ignores them.
2. **Deploy code that writes both shapes.** Write to the old and new columns; read the old one.
3. **Backfill** existing rows in small batches, to avoid long locks and log growth.
4. **Switch reads** to the new column, then **enforce** constraints (`NOT NULL`, FKs, checks) once every row complies.
5. **Contract.** Drop or rename the old column only in a **later** release, once no deployed version reads it.

Never rename or drop in the same release that introduces the replacement. `AddOrderRowVersion` is an example of a naturally safe expand step: SQL Server fills `rowversion` for existing rows, so no backfill is needed. `RestoreFulfilledOrderStatus` shows the backfill-then-enforce pattern.

### Rollback strategy

- **Reversible migrations** (most of these): `dotnet ef database update <PreviousMigration>`, or run the generated `*.down.sql`, then redeploy the previous app version.
- **Prefer roll-forward.** If a release is bad, ship a fix migration rather than running `Down()` against production data.
- **Irreversible steps** (dropping columns or tables, lossy type changes, data deletes):
  - take a **backup** first (`BACKUP DATABASE ... WITH COPY_ONLY`), or confirm point-in-time restore is available;
  - mark the migration as irreversible (`Down()` throws) so nobody assumes it can be reverted;
  - the recovery path is a restore to a point in time before the migration, plus replaying writes if needed. This is why destructive changes belong in the separate **contract** phase.

---

## Messaging

| Element | Name | Notes |
|---|---|---|
| Exchange | `orders.events` | topic, durable |
| Routing key | `order.created` | |
| Queue | `orders.order-created` | durable; `x-dead-letter-exchange = orders.dlx` |
| Retry queues | `…retry.5s`, `…retry.30s`, `…retry.2m` | TTL, then dead-lettered back to `orders.events` (exponential-ish backoff) |
| DLX / DLQ | `orders.dlx` → `orders.order-created.dlq` | messages that exhausted their retries, or poison messages |

Topology is declared in code by both the publisher and the consumer. Declaring it again is harmless.

**Delivery is at-least-once end to end:**

1. **Transactional outbox.** `POST /api/orders` writes the order and the `OrderCreated` event in **one transaction**, so there is no dual-write problem. If the broker is down, the order is still accepted and the event is sent later.
2. **Dispatcher.** A background service publishes unsent outbox rows every 2 s, with **publisher confirms**, as persistent messages. `MessageId` is the outbox row id, and the correlation id travels in the message header.
3. **Worker.**
   - Manual acks, `prefetch = 10` for backpressure.
   - **Idempotent:** the `ProcessedMessages (MessageId, Consumer)` primary key plus the status change commit in one `SaveChanges`. Duplicates are logged and acknowledged.
   - **Retry:** on failure, the message is republished to the next retry queue (`x-retry-count` header) and the original is acked. After 3 attempts it goes to the DLQ with an `x-last-error` header.
   - **Poison messages** (invalid JSON or missing MessageId) are nacked straight to the DLQ.
   - **Concurrency:** if a user changes the order while the worker is allocating, `rowversion` raises a conflict, the worker retries, and it reloads fresh state.

To demo retries and the DLQ, set `Worker:FailureRate` to `1.0` and create an order. The message moves through 5 s → 30 s → 2 min and then lands in the DLQ.

**Scaling notes:**
- Workers scale as competing consumers.
- With several API instances, dispatchers should claim outbox rows with `UPDLOCK, READPAST` to avoid double-publishing (duplicates would still be safe).
- Processed outbox rows and old `ProcessedMessages` / `IdempotencyRecords` should be purged after a retention window (all have indexes on their timestamps).

---

## Security

- **Auth:** `Auth:Mode`
  - `Entra` (default): `AddMicrosoftIdentityWebApi`, which validates issuer, audience, signature and lifetime, and maps the `roles` claim. Define the app roles `Orders.Read`, `Orders.Write` and `Orders.Admin` in the API app registration.
  - `Dev`: tokens from `dotnet user-jwts` (or the demo token in Docker).
- **Policies** (hierarchical: Admin ⊇ Write ⊇ Read). GETs require `Orders.Read`; POST/PUT require `Orders.Write`. A **fallback policy** requires an authenticated user, so a new endpoint is never accidentally public.
- **Secure defaults:**
  - HSTS (non-dev), HTTPS redirection.
  - `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, and a strict CSP on `/api`.
  - CORS restricted to the web origin, with only the needed headers exposed.
  - Correlation ids are validated against a strict pattern to prevent log injection.
- **Secrets:** User Secrets locally; `.env` (git-ignored) for Compose. Nothing real is committed.
- **Dependency scanning in CI:** `dotnet list package --vulnerable --include-transitive` and `npm audit --omit=dev --audit-level=high`.
  - `npm audit` gates on **runtime** dependencies only. A high-severity ReDoS advisory in `picomatch` (a transitive dependency of Vite and Vitest) currently has no upstream fix. It affects build tooling only and never ships, so it is tracked rather than force-downgrading Vite.
  - Vulnerable transitive NuGet packages (`System.Security.Cryptography.Pkcs`/`Xml`) were pinned to patched versions.

---

## Observability

- **Structured JSON logs** (console) with scopes.
- **Correlation id:** `X-Correlation-ID` is accepted or generated by middleware, echoed in the response, added to every log line and to Problem Details, stored on the outbox row, sent as the AMQP `correlation_id`, and logged by the worker. One id follows a request from the browser to the worker.
- **Health:** `/healthz` (liveness, process up) and `/readiness` (SQL + RabbitMQ). RabbitMQ reports **Degraded** rather than Unhealthy, because thanks to the outbox the API can still accept orders while the broker is down.
- **Metrics:** OpenTelemetry → Prometheus at `/metrics`, with `http.server.request.duration` (histogram, which also gives request counts).

---

## Testing

| Layer | Project / tool | What it covers |
|---|---|---|
| Unit | `Orders.Domain.Tests` (xUnit) | SADC pairing, CMA option, state machine (user vs system), totals, line validation |
| Integration | `Orders.API.IntegrationTests` (WebApplicationFactory) | Real SQL Server; the database is **created from the migrations** for each run. Covers create/get/search, validation 400, domain 422, duplicate 409, 401/403, ETag 304, **idempotent status update** (applied once + replay), key reuse 422, stale If-Match 412, **outbox publish** (fake publisher, row marked processed) |
| Component | Vitest + React Testing Library | Customers list and create form, pagination, Problem Details rendering |
| E2E | – | Not implemented (see below) |

```bash
dotnet test                         # needs a local SQL Server; uses DB AmrodOrders_IntegrationTests
cd web && npm test                  # or npm run test:coverage
```

Integration tests use a header-driven test auth handler (`X-Test-Roles`) and replace the RabbitMQ publisher with an in-memory fake. Set `TEST_SQL_CONNECTION` to point them at another server (CI does this).

The integration tests caught a real bug: controller error responses were served as `application/json` instead of `application/problem+json`, because a class-level `[Produces]` attribute overrode the content type.

---

## CI/CD

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) runs on every PR and on every push to `master`.

| Job | Steps |
|---|---|
| **API** | restore → build → `dotnet format --verify-no-changes` → vulnerable package scan → unit + integration tests (SQL Server service container) with coverage → coverage summary in the job summary → `dotnet ef migrations script --idempotent` → **apply the script twice to an empty database** → publish API and Worker → upload artifacts (binaries, SQL script, coverage, test results) |
| **Web** | `npm ci` → lint (oxlint) → typecheck → tests with coverage → build → `npm audit` (runtime deps) → upload `dist` and coverage |
| **Docker** | Builds all Compose images (runs after the other two pass) |

Branching: feature branches → pull requests → merge to `master`.

---

## SADC currencies and the Common Monetary Area

Validation uses ISO 3166-1 alpha-2 country codes and the ISO 4217 currencies permitted for each (`Orders.Domain/Sadc.cs`). Zimbabwe accepts both `ZWL` and `USD`.

**Common Monetary Area (ZA, NA, LS, SZ):**
- The NAD, LSL and SZL are pegged 1:1 to the ZAR, and the **rand is legal tender in Namibia, Lesotho and Eswatini**, but not the other way round.
- `Ordering:AllowRandInCma` (default `true`) lets NA/LS/SZ customers order in ZAR.
- **Practical implications:**
  - Never sum across currencies, even at-par ones. Reports group by currency (see `sp_GetTransactionReport`).
  - At-par conversion is safe for display, but settlement, tax and invoicing are still per currency and per country.
  - The peg is a policy, not a law of nature, so conversion should go through a rate table, not a hard-coded 1.0.

**Zimbabwe:** the brief specifies `ZWL`. Zimbabwe introduced the ZiG (ISO 4217 `ZWG`) in 2024. A production system would accept `ZWG`, and probably drop `ZWL` after a transition period. The table is data-driven, so this is a one-line change.

---

## Trade-offs and what I'd do with more time

**Deliberately skipped (optional in the brief):**
- **GraphQL** read endpoint (e.g. HotChocolate with projections and filtering).
- **Playwright E2E:** Create Customer → Create Order → Transition Status.

**Next improvements:**
- Automated test for the **worker's** idempotency and retry/DLQ path (currently verified manually by re-publishing a processed message and with `FailureRate = 1.0`).
- **Testcontainers** for SQL Server and RabbitMQ in integration tests, so they don't need a local SQL Server.
- Outbox claiming with `UPDLOCK, READPAST` for multiple API instances, plus scheduled cleanup of outbox, processed-message and idempotency rows.
- A product catalogue: SKUs are free text because the brief defines none. Allocation would reserve stock per SKU.
- A reference-data endpoint for SADC currencies, so the frontend doesn't duplicate the table.
- MSAL sign-in in the frontend instead of a dev token; a least-privilege SQL login instead of `sa`; secrets in Key Vault.
- Publish Docker images to a registry and add a deployment stage.

**Known quirks:**
- oxlint's directory scan found no files, because the repo `.gitignore` once excluded `/web` and oxlint honours `.gitignore`. Lint runs through `web/scripts/lint.mjs`, which passes the file list explicitly. CI also caught that the same `.gitignore` rule was hiding new frontend files from commits.
- TypeScript 7 (from the Vite template) isn't yet supported by `openapi-typescript`, so type generation runs through `npx` with its own TypeScript: `npm run gen:api`.

---

## Time spent

Approximately **16 hours** over 2 days. I prioritised the domain rules, migrations and database lifecycle, idempotency and concurrency, and reliable messaging, then the frontend, tests and CI. Written answers are in [`ANSWERS.md`](ANSWERS.md).
