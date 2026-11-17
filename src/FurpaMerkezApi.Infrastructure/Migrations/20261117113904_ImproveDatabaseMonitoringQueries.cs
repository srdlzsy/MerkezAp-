using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImproveDatabaseMonitoringQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_mobile_offline_sync_requests_operation_warehouse_updated",
                table: "mobile_offline_sync_requests",
                columns: new[] { "operation_code", "warehouse_no", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_mobile_offline_sync_requests_status_created",
                table: "mobile_offline_sync_requests",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_mobile_offline_sync_requests_status_updated",
                table: "mobile_offline_sync_requests",
                columns: new[] { "status", "updated_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mobile_offline_sync_requests_operation_warehouse_updated",
                table: "mobile_offline_sync_requests");

            migrationBuilder.DropIndex(
                name: "ix_mobile_offline_sync_requests_status_created",
                table: "mobile_offline_sync_requests");

            migrationBuilder.DropIndex(
                name: "ix_mobile_offline_sync_requests_status_updated",
                table: "mobile_offline_sync_requests");
        }
    }
}
