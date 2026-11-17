BEGIN TRANSACTION;
CREATE TABLE [IdempotencyRecords] (
    [Key] varchar(150) NOT NULL,
    [RequestHash] char(64) NOT NULL,
    [ResourceId] uniqueidentifier NOT NULL,
    [StatusCode] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY ([Key])
);

CREATE INDEX [IX_IdempotencyRecords_CreatedAt] ON [IdempotencyRecords] ([CreatedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261117192910_AddIdempotencyRecords', N'10.0.0');

COMMIT;
GO

