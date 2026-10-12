using System.Data;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace FurpaMerkezApi.Infrastructure.Modules.Common;

internal static class MikroStockMovementBulkWriter
{
    internal const int SingleBatchSize = 0;
    internal const SqlBulkCopyOptions RequiredOptions =
        SqlBulkCopyOptions.CheckConstraints |
        SqlBulkCopyOptions.FireTriggers;

    public static async Task WriteAsync(
        MikroWriteDbContext dbContext,
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(movements);

        if (movements.Count == 0)
        {
            return;
        }

        if (!dbContext.Database.IsSqlServer())
        {
            throw new InvalidOperationException("Mikro stock movement bulk writing requires SQL Server.");
        }

        var connection = dbContext.Database.GetDbConnection() as SqlConnection
            ?? throw new InvalidOperationException("Mikro write connection is not a SQL Server connection.");
        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction
            ?? throw new InvalidOperationException("Mikro stock movement bulk writing requires an active SQL transaction.");

        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Mikro stock movement bulk writing requires an open SQL connection.");
        }

        using var dataTable = BuildDataTable(dbContext, movements);
        using var bulkCopy = new SqlBulkCopy(connection, RequiredOptions, transaction)
        {
            DestinationTableName = "[dbo].[STOK_HAREKETLERI]",
            BatchSize = SingleBatchSize,
            BulkCopyTimeout = Math.Clamp(timeoutSeconds, 1, 600)
        };

        foreach (DataColumn column in dataTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
    }

    internal static DataTable BuildDataTable(
        MikroWriteDbContext dbContext,
        IReadOnlyCollection<STOK_HAREKETLERI> movements)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(movements);

        var entityType = dbContext.Model.FindEntityType(typeof(STOK_HAREKETLERI))
            ?? throw new InvalidOperationException("STOK_HAREKETLERI metadata was not found.");
        var storeObject = StoreObjectIdentifier.Table(
            entityType.GetTableName() ?? "STOK_HAREKETLERI",
            entityType.GetSchema());
        var properties = entityType
            .GetProperties()
            .Where(property =>
                !property.IsShadowProperty() &&
                property.PropertyInfo is not null &&
                property.GetBeforeSaveBehavior() == PropertySaveBehavior.Save)
            .Select(property => new BulkProperty(
                property.GetColumnName(storeObject)
                    ?? throw new InvalidOperationException($"Column mapping was not found for {property.Name}."),
                Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType,
                property.PropertyInfo!))
            .ToArray();

        var table = new DataTable(entityType.GetTableName() ?? "STOK_HAREKETLERI");
        foreach (var property in properties)
        {
            table.Columns.Add(property.ColumnName, property.DataType);
        }

        foreach (var movement in movements)
        {
            var row = table.NewRow();
            foreach (var property in properties)
            {
                row[property.ColumnName] = property.PropertyInfo.GetValue(movement) ?? DBNull.Value;
            }

            table.Rows.Add(row);
        }

        return table;
    }

    private sealed record BulkProperty(
        string ColumnName,
        Type DataType,
        System.Reflection.PropertyInfo PropertyInfo);
}
