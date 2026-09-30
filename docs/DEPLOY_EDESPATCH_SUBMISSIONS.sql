BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930082411_AddDurableEDespatchSubmissions'
)
BEGIN
    CREATE TABLE [edespatch_submissions] (
        [id] uniqueidentifier NOT NULL,
        [document_key] nvarchar(180) NOT NULL,
        [document_no] nvarchar(50) NOT NULL,
        [uuid] nvarchar(50) NOT NULL,
        [payload_json] nvarchar(max) NOT NULL,
        [status] nvarchar(30) NOT NULL,
        [attempt_count] int NOT NULL,
        [created_at_utc] datetime2 NOT NULL,
        [next_attempt_at_utc] datetime2 NOT NULL,
        [completed_at_utc] datetime2 NULL,
        [last_error] nvarchar(2000) NULL,
        CONSTRAINT [PK_edespatch_submissions] PRIMARY KEY ([id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930082411_AddDurableEDespatchSubmissions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_edespatch_submissions_document_key] ON [edespatch_submissions] ([document_key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930082411_AddDurableEDespatchSubmissions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_edespatch_submissions_document_no] ON [edespatch_submissions] ([document_no]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930082411_AddDurableEDespatchSubmissions'
)
BEGIN
    CREATE INDEX [IX_edespatch_submissions_status_next_attempt_at_utc] ON [edespatch_submissions] ([status], [next_attempt_at_utc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930082411_AddDurableEDespatchSubmissions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_edespatch_submissions_uuid] ON [edespatch_submissions] ([uuid]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930082411_AddDurableEDespatchSubmissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930082411_AddDurableEDespatchSubmissions', N'9.0.19');
END;

COMMIT;
GO

