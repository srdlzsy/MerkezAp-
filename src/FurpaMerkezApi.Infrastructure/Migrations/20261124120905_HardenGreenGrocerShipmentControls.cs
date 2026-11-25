using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenGreenGrocerShipmentControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_order_linking",
                table: "green_grocer_order_line_snapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "over_delivery_tolerance_percent",
                table: "green_grocer_order_line_snapshots",
                type: "float",
                nullable: false,
                defaultValue: 20.0);

            migrationBuilder.AddColumn<bool>(
                name: "requires_manual_approval",
                table: "green_grocer_order_line_snapshots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "severe_over_delivery_multiplier",
                table: "green_grocer_order_line_snapshots",
                type: "float",
                nullable: false,
                defaultValue: 1.5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allow_order_linking",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "over_delivery_tolerance_percent",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "requires_manual_approval",
                table: "green_grocer_order_line_snapshots");

            migrationBuilder.DropColumn(
                name: "severe_over_delivery_multiplier",
                table: "green_grocer_order_line_snapshots");
        }
    }
}
