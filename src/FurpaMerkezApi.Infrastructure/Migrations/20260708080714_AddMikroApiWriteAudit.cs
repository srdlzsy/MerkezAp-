using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMikroApiWriteAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mikro_api_write_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    request_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_flow_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    correlation_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    payload_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    http_status_code = table.Column<int>(type: "int", nullable: true),
                    mikro_status_code = table.Column<int>(type: "int", nullable: true),
                    response = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    attempt_count = table.Column<int>(type: "int", nullable: false),
                    elapsed_milliseconds = table.Column<long>(type: "bigint", nullable: true),
                    recovered_document_no = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    recovered_guid = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    recovered_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mikro_api_write_audits", x => x.id);
                    table.ForeignKey(
                        name: "FK_mikro_api_write_audits_document_flows_document_flow_id",
                        column: x => x.document_flow_id,
                        principalTable: "document_flows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mikro_api_write_audits_correlation_id",
                table: "mikro_api_write_audits",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_mikro_api_write_audits_flow_created",
                table: "mikro_api_write_audits",
                columns: new[] { "document_flow_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_mikro_api_write_audits_status_created",
                table: "mikro_api_write_audits",
                columns: new[] { "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_mikro_api_write_audits_request_id",
                table: "mikro_api_write_audits",
                column: "request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mikro_api_write_audits");
        }
    }
}
