using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGreenGrocerV2SnapshotAndCaseTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "actual_case_count",
                table: "green_grocer_shipment_line_records",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "approved_kg_per_case",
                table: "green_grocer_order_line_snapshots",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "average_window_days_used",
                table: "green_grocer_order_line_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calculation_tier",
                table: "green_grocer_order_line_snapshots",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "drift_percent",
                table: "green_grocer_order_line_snapshots",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "label_age_days",
                table: "green_grocer_order_line_snapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "manual_approval_confirmed",
                table: "green_grocer_order_line_snapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "prior_average_kg_per_case",
                table: "green_grocer_order_line_snapshots",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actual_case_count",
                table: "green_grocer_shipment_line_records");

            migrationBuilder.DropColumn(
                name: "approved_kg_per_case",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "average_window_days_used",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "calculation_tier",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "drift_percent",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "label_age_days",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "manual_approval_confirmed",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "prior_average_kg_per_case",
                table: "green_grocer_order_line_snapshots");
        }
    }
}
