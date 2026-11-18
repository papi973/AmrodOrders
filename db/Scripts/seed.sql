/*
  Seed data: 80 customers (5 per SADC country) and 1,000 orders with 1-4 line items each,
  spread across all SADC countries and their permitted currencies (incl. ZAR for CMA countries
  and USD for Zimbabwe), statuses and the last 180 days.

  - Safe to re-run: does nothing if there are already 1,000+ orders.
  - Deterministic distribution (no random CASE pitfalls), set-based (runs in about a second).
  - Prices are illustrative and not converted between currencies.

  Run:
    Local (Windows auth):  sqlcmd -S localhost -d AmrodOrders -E -I -i db/scripts/seed.sql
    Docker:                runs automatically via the `seed` service in docker-compose.yml
    SSMS:                  open this file, select the AmrodOrders database, Execute
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF (SELECT COUNT(*) FROM dbo.Orders) >= 1000
BEGIN
    PRINT 'Seed skipped: 1,000+ orders already exist.';
    RETURN;
END;

BEGIN TRAN;

-- Allowed country/currency pairs (mirrors Orders.Domain.Sadc, with CMA rand enabled)
DECLARE @Pairs TABLE (CountryCode char(2), CountryName nvarchar(50), CurrencyCode char(3));
INSERT @Pairs (CountryCode, CountryName, CurrencyCode) VALUES
    ('AO', N'Angola', 'AOA'), ('BW', N'Botswana', 'BWP'), ('KM', N'Comoros', 'KMF'),
    ('CD', N'DR Congo', 'CDF'), ('SZ', N'Eswatini', 'SZL'), ('SZ', N'Eswatini', 'ZAR'),
    ('LS', N'Lesotho', 'LSL'), ('LS', N'Lesotho', 'ZAR'), ('MG', N'Madagascar', 'MGA'),
    ('MW', N'Malawi', 'MWK'), ('MU', N'Mauritius', 'MUR'), ('MZ', N'Mozambique', 'MZN'),
    ('NA', N'Namibia', 'NAD'), ('NA', N'Namibia', 'ZAR'), ('SC', N'Seychelles', 'SCR'),
    ('ZA', N'South Africa', 'ZAR'), ('TZ', N'Tanzania', 'TZS'), ('ZM', N'Zambia', 'ZMW'),
    ('ZW', N'Zimbabwe', 'ZWL'), ('ZW', N'Zimbabwe', 'USD');

-- 1. Customers: 5 per country
DECLARE @Customers TABLE (Id uniqueidentifier PRIMARY KEY, CountryCode char(2));

INSERT dbo.Customers (Id, Name, Email, CountryCode, CreatedAt)
OUTPUT inserted.Id, inserted.CountryCode INTO @Customers (Id, CountryCode)
SELECT NEWID(),
       CONCAT(c.CountryName, N' Trading ', n.n),
       CONCAT('seed.', LOWER(c.CountryCode), '.', n.n, '.', LEFT(CONVERT(varchar(36), NEWID()), 8), '@example.com'),
       c.CountryCode,
       DATEADD(DAY, -200, SYSUTCDATETIME())
FROM (SELECT DISTINCT CountryCode, CountryName FROM @Pairs) AS c
CROSS JOIN (VALUES (1), (2), (3), (4), (5)) AS n(n);

DECLARE @CustomerCount int = (SELECT COUNT(*) FROM @Customers);

-- 2. Orders: 1,000, spread over customers, currencies, statuses and 180 days
DECLARE @Orders TABLE (Id uniqueidentifier PRIMARY KEY);

WITH numbers AS (
    SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
    FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b
),
customers AS (
    SELECT Id, CountryCode, ROW_NUMBER() OVER (ORDER BY Id) AS rn FROM @Customers
),
currencies AS (
    SELECT CountryCode, CurrencyCode,
           ROW_NUMBER() OVER (PARTITION BY CountryCode ORDER BY CurrencyCode) AS rn,
           COUNT(*)     OVER (PARTITION BY CountryCode)                       AS cnt
    FROM @Pairs
)
INSERT dbo.Orders (Id, CustomerId, Status, CreatedAt, CurrencyCode, TotalAmount)
OUTPUT inserted.Id INTO @Orders (Id)
SELECT NEWID(),
       c.Id,
       CASE n.i % 10                      -- 60% Fulfilled, 20% Paid, 10% Pending, 10% Cancelled
           WHEN 0 THEN 'Pending'
           WHEN 1 THEN 'Cancelled'
           WHEN 2 THEN 'Paid'
           WHEN 3 THEN 'Paid'
           ELSE 'Fulfilled'
       END,
       DATEADD(MINUTE, -((n.i * 7919) % (180 * 24 * 60)), SYSUTCDATETIME()),
       cur.CurrencyCode,
       0                                  -- calculated from line items below
FROM numbers AS n
JOIN customers  AS c   ON c.rn = (n.i % @CustomerCount) + 1
JOIN currencies AS cur ON cur.CountryCode = c.CountryCode
                      AND cur.rn = ((n.i / @CustomerCount) % cur.cnt) + 1;

-- 3. Line items: 1-4 per order, distinct SKUs within an order
WITH orders AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS rn FROM @Orders
),
skus AS (
    SELECT * FROM (VALUES
        (0, 'PEN-BLUE',        12.50),
        (1, 'MUG-WHITE-330',   45.00),
        (2, 'CAP-NAVY',        89.99),
        (3, 'TSHIRT-BLK-L',   149.00),
        (4, 'BOTTLE-SS-500',  199.95),
        (5, 'NOTEBOOK-A5',     35.00)
    ) AS s(n, Sku, Price)
)
INSERT dbo.OrderLineItems (Id, OrderId, ProductSku, Quantity, UnitPrice)
SELECT NEWID(), o.Id, s.Sku, ((o.rn + k.k) % 20) + 1, s.Price
FROM orders AS o
CROSS JOIN (VALUES (1), (2), (3), (4)) AS k(k)
JOIN skus AS s ON s.n = (o.rn + k.k) % 6
WHERE k.k <= (o.rn % 4) + 1;

-- 4. Totals computed from line items (same rule as the API: sum of Quantity x UnitPrice)
UPDATE o
SET TotalAmount = t.Total
FROM dbo.Orders AS o
JOIN (
    SELECT OrderId, SUM(Quantity * UnitPrice) AS Total
    FROM dbo.OrderLineItems
    WHERE OrderId IN (SELECT Id FROM @Orders)
    GROUP BY OrderId
) AS t ON t.OrderId = o.Id;

COMMIT;

DECLARE @SeededCustomers int, @SeededOrders int;
SELECT @SeededCustomers = COUNT(*) FROM @Customers;
SELECT @SeededOrders = COUNT(*) FROM @Orders;
PRINT CONCAT('Seeded ', @SeededCustomers, ' customers and ', @SeededOrders, ' orders.');
