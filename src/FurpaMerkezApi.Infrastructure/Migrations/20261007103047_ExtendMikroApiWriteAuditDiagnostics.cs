using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMikroApiWriteAuditDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "mikro_api_write_audits",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<int>(
                name: "document_order_no",
                table: "mikro_api_write_audits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_serie",
                table: "mikro_api_write_audits",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "line_count",
                table: "mikro_api_write_audits",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_source",
                table: "mikro_api_write_audits",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "warehouse_no",
                table: "mikro_api_write_audits",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_mikro_api_write_audits_document_created",
                table: "mikro_api_write_audits",
                columns: new[] { "document_serie", "document_order_no", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_mikro_api_write_audits_warehouse_created",
                table: "mikro_api_write_audits",
                columns: new[] { "warehouse_no", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mikro_api_write_audits_document_created",
                table: "mikro_api_write_audits");

            migrationBuilder.DropIndex(
                name: "ix_mikro_api_write_audits_warehouse_created",
                table: "mikro_api_write_audits");

            migrationBuilder.DropColumn(
                name: "document_order_no",
                table: "mikro_api_write_audits");

            migrationBuilder.DropColumn(
                name: "document_serie",
                table: "mikro_api_write_audits");

            migrationBuilder.DropColumn(
                name: "line_count",
                table: "mikro_api_write_audits");

            migrationBuilder.DropColumn(
                name: "result_source",
                table: "mikro_api_write_audits");

            migrationBuilder.DropColumn(
                name: "warehouse_no",
                table: "mikro_api_write_audits");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "mikro_api_write_audits",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
