using System;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260506170000_AddMobileOfflineSyncRequests")]
    public partial class AddMobileOfflineSyncRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mobile_offline_sync_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_no = table.Column<int>(type: "integer", nullable: false),
                    client_request_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_payload = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    response_payload = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_offline_sync_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_mobile_offline_sync_requests_operation_user_request",
                table: "mobile_offline_sync_requests",
                columns: new[] { "operation_code", "requested_by_user_id", "client_request_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mobile_offline_sync_requests");
        }
    }
}
