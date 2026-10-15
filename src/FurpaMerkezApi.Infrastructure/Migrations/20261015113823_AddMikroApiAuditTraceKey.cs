using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMikroApiAuditTraceKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trace_key",
                table: "mikro_api_write_audits",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_mikro_api_write_audits_trace_status",
                table: "mikro_api_write_audits",
                columns: new[] { "trace_key", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mikro_api_write_audits_trace_status",
                table: "mikro_api_write_audits");

            migrationBuilder.DropColumn(
                name: "trace_key",
                table: "mikro_api_write_audits");
        }
    }
}
