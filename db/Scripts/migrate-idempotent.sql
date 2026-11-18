IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [CountryCode] char(2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Customers_CountryCode_Length] CHECK (LEN([CountryCode]) = 2)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] uniqueidentifier NOT NULL,
        [Status] varchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CurrencyCode] char(3) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN ('Pending', 'Paid', 'Delivered', 'Cancelled')),
        CONSTRAINT [CK_Orders_TotalAmount_NonNegative] CHECK ([TotalAmount] >= 0),
        CONSTRAINT [FK_Orders_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderLineItems] (
        [Id] uniqueidentifier NOT NULL,
        [OrderId] uniqueidentifier NOT NULL,
        [ProductSku] varchar(64) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderLineItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_OrderLineItems_Quantity_Positive] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_OrderLineItems_UnitPrice_NonNegative] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [FK_OrderLineItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Customers_Name] ON [Customers] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Customers_Email] ON [Customers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderLineItems_OrderId] ON [OrderLineItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_CreatedAt] ON [Orders] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_CustomerId_Status_CreatedAt] ON [Orders] ([CustomerId], [Status], [CreatedAt]) INCLUDE ([TotalAmount], [CurrencyCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117174354_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117174354_InitialCreate', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117180325_AddOrderRowVersion'
)
BEGIN
    ALTER TABLE [Orders] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117180325_AddOrderRowVersion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117180325_AddOrderRowVersion', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117192910_AddIdempotencyRecords'
)
BEGIN
    CREATE TABLE [IdempotencyRecords] (
        [Key] varchar(150) NOT NULL,
        [RequestHash] char(64) NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [StatusCode] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY ([Key])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117192910_AddIdempotencyRecords'
)
BEGIN
    CREATE INDEX [IX_IdempotencyRecords_CreatedAt] ON [IdempotencyRecords] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117192910_AddIdempotencyRecords'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117192910_AddIdempotencyRecords', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118070957_AddOutboxAndProcessedMessages'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [Type] nvarchar(200) NOT NULL,
        [RoutingKey] varchar(200) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CorrelationId] varchar(64) NULL,
        [OccurredAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(2000) NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118070957_AddOutboxAndProcessedMessages'
)
BEGIN
    CREATE TABLE [ProcessedMessages] (
        [MessageId] uniqueidentifier NOT NULL,
        [Consumer] varchar(100) NOT NULL,
        [ProcessedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProcessedMessages] PRIMARY KEY ([MessageId], [Consumer])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118070957_AddOutboxAndProcessedMessages'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_OutboxMessages_Unprocessed] ON [OutboxMessages] ([OccurredAt]) WHERE [ProcessedAt] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118070957_AddOutboxAndProcessedMessages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118070957_AddOutboxAndProcessedMessages', N'10.0.0');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118074404_RestoreFulfilledOrderStatus'
)
BEGIN
    ALTER TABLE [Orders] DROP CONSTRAINT [CK_Orders_Status];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118074404_RestoreFulfilledOrderStatus'
)
BEGIN
    EXEC(N'ALTER TABLE [Orders] ADD CONSTRAINT [CK_Orders_Status] CHECK ([Status] IN (''Pending'', ''Paid'', ''Fulfilled'', ''Cancelled''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118074404_RestoreFulfilledOrderStatus'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118074404_RestoreFulfilledOrderStatus', N'10.0.0');
END;

COMMIT;
GO

