BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'RowVersion');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Orders] DROP COLUMN [RowVersion];

DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20261117180325_AddOrderRowVersion';

COMMIT;
GO

