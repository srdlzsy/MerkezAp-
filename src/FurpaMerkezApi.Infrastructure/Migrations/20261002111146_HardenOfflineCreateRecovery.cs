using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenOfflineCreateRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_code",
                table: "mobile_offline_sync_requests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "retryable",
                table: "mobile_offline_sync_requests",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revision",
                table: "mobile_offline_sync_requests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Preserve the manual-review decision of failures written before typed errors existed.
            migrationBuilder.Sql("""
                UPDATE mobile_offline_sync_requests
                SET error_code = 'MIKRO_DOCUMENT_CONTENT_MISMATCH', retryable = 0
                WHERE status = 'Failed'
                  AND error_message LIKE 'The existing Mikro document does not match the requested document content.%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "error_code",
                table: "mobile_offline_sync_requests");

            migrationBuilder.DropColumn(
                name: "retryable",
                table: "mobile_offline_sync_requests");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "mobile_offline_sync_requests");
        }
    }
}
