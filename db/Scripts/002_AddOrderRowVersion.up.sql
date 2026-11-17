BEGIN TRANSACTION;
ALTER TABLE [Orders] ADD [RowVersion] rowversion NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261117180325_AddOrderRowVersion', N'10.0.0');

COMMIT;
GO

