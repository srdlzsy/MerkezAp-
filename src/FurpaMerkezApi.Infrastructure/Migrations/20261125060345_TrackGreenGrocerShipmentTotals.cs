using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrackGreenGrocerShipmentTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "green_grocer_shipment_line_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    order_line_snapshot_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    warehouse_order_line_guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_serie = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    document_order_no = table.Column<int>(type: "int", nullable: false),
                    actual_quantity = table.Column<double>(type: "float", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_green_grocer_shipment_line_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_green_grocer_shipment_line_records_green_grocer_order_line_snapshots_order_line_snapshot_id",
                        column: x => x.order_line_snapshot_id,
                        principalTable: "green_grocer_order_line_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_green_grocer_shipment_records_snapshot",
                table: "green_grocer_shipment_line_records",
                column: "order_line_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ux_green_grocer_shipment_records_request_line",
                table: "green_grocer_shipment_line_records",
                columns: new[] { "client_request_id", "warehouse_order_line_guid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "green_grocer_shipment_line_records");
        }
    }
}
