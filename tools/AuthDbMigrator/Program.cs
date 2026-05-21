using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

var sourceConnectionString = Environment.GetEnvironmentVariable("SOURCE_AUTH_CONNECTION");
var targetConnectionString = Environment.GetEnvironmentVariable("TARGET_AUTH_CONNECTION");

if (string.IsNullOrWhiteSpace(sourceConnectionString))
{
    throw new InvalidOperationException("SOURCE_AUTH_CONNECTION environment variable is required.");
}

if (string.IsNullOrWhiteSpace(targetConnectionString))
{
    throw new InvalidOperationException("TARGET_AUTH_CONNECTION environment variable is required.");
}

await using var source = new NpgsqlConnection(sourceConnectionString);
await using var target = new SqlConnection(targetConnectionString);

await source.OpenAsync();
await target.OpenAsync();

await EnsureSchemaAsync(target);
await EnsureTargetIsEmptyAsync(target);

var tables = new[]
{
    new TableCopy("__EFMigrationsHistory", new[] { "MigrationId", "ProductVersion" }, QuotePostgresIdentifiers: true),
    new TableCopy("app_permissions", new[] { "id", "code", "name", "description", "created_at_utc", "updated_at_utc" }),
    new TableCopy("app_roles", new[] { "id", "name", "description", "is_active", "created_at_utc", "updated_at_utc" }),
    new TableCopy("app_users", new[] { "id", "username", "normalized_username", "email", "normalized_email", "first_name", "last_name", "warehouse_no", "warehouse_name", "password_hash", "is_active", "created_at_utc", "updated_at_utc" }),
    new TableCopy("app_role_permissions", new[] { "role_id", "permission_id", "assigned_at_utc" }),
    new TableCopy("app_user_roles", new[] { "user_id", "role_id", "assigned_at_utc" }),
    new TableCopy("mobile_offline_sync_requests", new[] { "id", "operation_code", "requested_by_user_id", "warehouse_no", "client_request_id", "request_fingerprint", "request_payload", "status", "response_payload", "error_message", "created_at_utc", "updated_at_utc", "completed_at_utc" }),
    new TableCopy("uyumsoft_inbox_invoices", new[] { "id", "document_id", "invoice_id", "service_document_id", "local_document_id", "customer_title", "customer_tckn_vkn", "create_date", "invoice_date", "invoice_type", "invoice_total", "despatch_id", "is_processed", "is_printed", "is_standard", "status_code", "status", "envelope_status_code", "created_at_utc", "updated_at_utc", "last_synchronized_at_utc" })
};

foreach (var table in tables)
{
    await CopyTableAsync(source, target, table);
}

Console.WriteLine("Auth DB migration completed.");

static async Task EnsureSchemaAsync(SqlConnection connection)
{
    await ExecuteAsync(
        connection,
        """
        IF OBJECT_ID(N'[dbo].[app_role_permissions]', N'U') IS NOT NULL
            RETURN;

        CREATE TABLE [dbo].[__EFMigrationsHistory] (
            [MigrationId] nvarchar(150) NOT NULL,
            [ProductVersion] nvarchar(32) NOT NULL,
            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
        );

        CREATE TABLE [dbo].[app_permissions] (
            [id] uniqueidentifier NOT NULL,
            [code] nvarchar(100) NOT NULL,
            [name] nvarchar(100) NOT NULL,
            [description] nvarchar(250) NULL,
            [created_at_utc] datetime2 NOT NULL,
            [updated_at_utc] datetime2 NULL,
            CONSTRAINT [PK_app_permissions] PRIMARY KEY ([id])
        );

        CREATE TABLE [dbo].[app_roles] (
            [id] uniqueidentifier NOT NULL,
            [name] nvarchar(100) NOT NULL,
            [description] nvarchar(250) NULL,
            [is_active] bit NOT NULL,
            [created_at_utc] datetime2 NOT NULL,
            [updated_at_utc] datetime2 NULL,
            CONSTRAINT [PK_app_roles] PRIMARY KEY ([id])
        );

        CREATE TABLE [dbo].[app_users] (
            [id] uniqueidentifier NOT NULL,
            [username] nvarchar(50) NOT NULL,
            [normalized_username] nvarchar(50) NOT NULL,
            [email] nvarchar(200) NOT NULL,
            [normalized_email] nvarchar(200) NOT NULL,
            [first_name] nvarchar(100) NOT NULL,
            [last_name] nvarchar(100) NOT NULL,
            [warehouse_no] nvarchar(50) NOT NULL,
            [warehouse_name] nvarchar(150) NOT NULL,
            [password_hash] nvarchar(500) NOT NULL,
            [is_active] bit NOT NULL,
            [created_at_utc] datetime2 NOT NULL,
            [updated_at_utc] datetime2 NULL,
            CONSTRAINT [PK_app_users] PRIMARY KEY ([id])
        );

        CREATE TABLE [dbo].[app_role_permissions] (
            [role_id] uniqueidentifier NOT NULL,
            [permission_id] uniqueidentifier NOT NULL,
            [assigned_at_utc] datetime2 NOT NULL,
            CONSTRAINT [PK_app_role_permissions] PRIMARY KEY ([role_id], [permission_id]),
            CONSTRAINT [FK_app_role_permissions_app_permissions_permission_id] FOREIGN KEY ([permission_id]) REFERENCES [dbo].[app_permissions] ([id]) ON DELETE CASCADE,
            CONSTRAINT [FK_app_role_permissions_app_roles_role_id] FOREIGN KEY ([role_id]) REFERENCES [dbo].[app_roles] ([id]) ON DELETE CASCADE
        );

        CREATE TABLE [dbo].[app_user_roles] (
            [user_id] uniqueidentifier NOT NULL,
            [role_id] uniqueidentifier NOT NULL,
            [assigned_at_utc] datetime2 NOT NULL,
            CONSTRAINT [PK_app_user_roles] PRIMARY KEY ([user_id], [role_id]),
            CONSTRAINT [FK_app_user_roles_app_roles_role_id] FOREIGN KEY ([role_id]) REFERENCES [dbo].[app_roles] ([id]) ON DELETE CASCADE,
            CONSTRAINT [FK_app_user_roles_app_users_user_id] FOREIGN KEY ([user_id]) REFERENCES [dbo].[app_users] ([id]) ON DELETE CASCADE
        );

        CREATE TABLE [dbo].[mobile_offline_sync_requests] (
            [id] uniqueidentifier NOT NULL,
            [operation_code] nvarchar(100) NOT NULL,
            [requested_by_user_id] uniqueidentifier NOT NULL,
            [warehouse_no] int NOT NULL,
            [client_request_id] nvarchar(50) NOT NULL,
            [request_fingerprint] nvarchar(64) NOT NULL,
            [request_payload] nvarchar(max) NULL,
            [status] nvarchar(20) NOT NULL,
            [response_payload] nvarchar(max) NULL,
            [error_message] nvarchar(max) NULL,
            [created_at_utc] datetime2 NOT NULL,
            [updated_at_utc] datetime2 NULL,
            [completed_at_utc] datetime2 NULL,
            CONSTRAINT [PK_mobile_offline_sync_requests] PRIMARY KEY ([id])
        );

        CREATE TABLE [dbo].[uyumsoft_inbox_invoices] (
            [id] uniqueidentifier NOT NULL,
            [document_id] nvarchar(150) NOT NULL,
            [invoice_id] nvarchar(150) NOT NULL,
            [service_document_id] nvarchar(150) NULL,
            [local_document_id] nvarchar(250) NULL,
            [customer_title] nvarchar(255) NOT NULL,
            [customer_tckn_vkn] nvarchar(50) NOT NULL,
            [create_date] datetime2 NULL,
            [invoice_date] datetime2 NULL,
            [invoice_type] nvarchar(80) NOT NULL,
            [invoice_total] decimal(18, 2) NOT NULL,
            [despatch_id] nvarchar(150) NOT NULL,
            [is_processed] bit NOT NULL,
            [is_printed] bit NOT NULL,
            [is_standard] bit NOT NULL,
            [status_code] nvarchar(80) NOT NULL,
            [status] nvarchar(120) NOT NULL,
            [envelope_status_code] nvarchar(80) NULL,
            [created_at_utc] datetime2 NOT NULL,
            [updated_at_utc] datetime2 NOT NULL,
            [last_synchronized_at_utc] datetime2 NOT NULL,
            CONSTRAINT [PK_uyumsoft_inbox_invoices] PRIMARY KEY ([id])
        );

        CREATE UNIQUE INDEX [ux_app_permissions_code] ON [dbo].[app_permissions] ([code]);
        CREATE UNIQUE INDEX [ux_app_roles_name] ON [dbo].[app_roles] ([name]);
        CREATE UNIQUE INDEX [ux_app_users_normalized_email] ON [dbo].[app_users] ([normalized_email]);
        CREATE UNIQUE INDEX [ux_app_users_normalized_username] ON [dbo].[app_users] ([normalized_username]);
        CREATE INDEX [IX_app_role_permissions_permission_id] ON [dbo].[app_role_permissions] ([permission_id]);
        CREATE INDEX [IX_app_user_roles_role_id] ON [dbo].[app_user_roles] ([role_id]);
        CREATE UNIQUE INDEX [ux_mobile_offline_sync_requests_operation_user_request] ON [dbo].[mobile_offline_sync_requests] ([operation_code], [requested_by_user_id], [client_request_id]);
        CREATE UNIQUE INDEX [ux_uyumsoft_inbox_invoices_document_id] ON [dbo].[uyumsoft_inbox_invoices] ([document_id]);
        CREATE INDEX [ix_uyumsoft_inbox_invoices_invoice_date] ON [dbo].[uyumsoft_inbox_invoices] ([invoice_date]);
        CREATE INDEX [ix_uyumsoft_inbox_invoices_processed_printed] ON [dbo].[uyumsoft_inbox_invoices] ([is_processed], [is_printed]);
        """);
}

static async Task EnsureTargetIsEmptyAsync(SqlConnection connection)
{
    const string sql =
        """
        SELECT SUM(row_count)
        FROM (
            SELECT COUNT_BIG(*) AS row_count FROM [dbo].[app_permissions]
            UNION ALL SELECT COUNT_BIG(*) FROM [dbo].[app_roles]
            UNION ALL SELECT COUNT_BIG(*) FROM [dbo].[app_users]
            UNION ALL SELECT COUNT_BIG(*) FROM [dbo].[app_role_permissions]
            UNION ALL SELECT COUNT_BIG(*) FROM [dbo].[app_user_roles]
            UNION ALL SELECT COUNT_BIG(*) FROM [dbo].[mobile_offline_sync_requests]
            UNION ALL SELECT COUNT_BIG(*) FROM [dbo].[uyumsoft_inbox_invoices]
        ) counts;
        """;

    await using var command = new SqlCommand(sql, connection);
    var result = await command.ExecuteScalarAsync();
    var rowCount = Convert.ToInt64(result ?? 0);

    if (rowCount > 0)
    {
        throw new InvalidOperationException($"Target Auth DB is not empty. Refusing to copy over {rowCount} existing rows.");
    }
}

static async Task CopyTableAsync(NpgsqlConnection source, SqlConnection target, TableCopy table)
{
    var sourceColumns = string.Join(", ", table.Columns.Select(column => table.QuotePostgresIdentifiers ? QuotePostgres(column) : column));
    var targetTable = QuoteSqlServer(table.Name);
    var sql = $"SELECT {sourceColumns} FROM {QuotePostgres(table.Name)}";

    await using var command = new NpgsqlCommand(sql, source);
    await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
    using var bulkCopy = new SqlBulkCopy(target, SqlBulkCopyOptions.KeepIdentity, null)
    {
        DestinationTableName = targetTable,
        BatchSize = 1000,
        BulkCopyTimeout = 300,
        EnableStreaming = true
    };

    foreach (var column in table.Columns)
    {
        bulkCopy.ColumnMappings.Add(column, column);
    }

    await bulkCopy.WriteToServerAsync(reader);

    await reader.CloseAsync();
    Console.WriteLine($"{table.Name}: copied");
}

static async Task ExecuteAsync(SqlConnection connection, string sql)
{
    await using var command = new SqlCommand(sql, connection)
    {
        CommandTimeout = 300
    };

    await command.ExecuteNonQueryAsync();
}

static string QuotePostgres(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

static string QuoteSqlServer(string identifier) => "[dbo].[" + identifier.Replace("]", "]]") + "]";

internal sealed record TableCopy(string Name, IReadOnlyCollection<string> Columns, bool QuotePostgresIdentifiers = false);
