using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260707130000_AddStockAnomalyProductManagers")]
public partial class AddStockAnomalyProductManagers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var isSqlServer = ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);

        migrationBuilder.AddColumn<string>(
            name: "product_manager_code",
            table: "stock_anomalies",
            type: isSqlServer ? "nvarchar(25)" : "character varying(25)",
            maxLength: 25,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "product_manager_name",
            table: "stock_anomalies",
            type: isSqlServer ? "nvarchar(120)" : "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_stock_anomalies_manager_status_last_detected",
            table: "stock_anomalies",
            columns: new[] { "product_manager_code", "status", "last_detected_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_stock_anomalies_manager_status_last_detected",
            table: "stock_anomalies");

        migrationBuilder.DropColumn(
            name: "product_manager_code",
            table: "stock_anomalies");

        migrationBuilder.DropColumn(
            name: "product_manager_name",
            table: "stock_anomalies");
    }
}
