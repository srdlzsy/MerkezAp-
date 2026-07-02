using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260702093000_ExtendUyumsoftInboxInvoiceCache")]
    public partial class ExtendUyumsoftInboxInvoiceCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isSqlServer = IsSqlServer();

            migrationBuilder.AddColumn<string>(
                name: "document_currency_code",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "nvarchar(10)" : "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "envelope_identifier",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "nvarchar(150)" : "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<decimal>(
                name: "exchange_rate",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "decimal(18,6)" : "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "bit" : "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_seen",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "bit" : "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_tip_type",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "nvarchar(80)" : "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<int>(
                name: "invoice_tip_type_code",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "int" : "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "message",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "nvarchar(500)" : "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "order_document_id",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "nvarchar(150)" : "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_exclusive_amount",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "decimal(18,2)" : "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_total",
                table: "uyumsoft_inbox_invoices",
                type: isSqlServer ? "decimal(18,2)" : "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_uyumsoft_inbox_invoices_order_document_id",
                table: "uyumsoft_inbox_invoices",
                column: "order_document_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_uyumsoft_inbox_invoices_order_document_id",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "document_currency_code",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "envelope_identifier",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "exchange_rate",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "is_seen",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "invoice_tip_type",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "invoice_tip_type_code",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "message",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "order_document_id",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "tax_exclusive_amount",
                table: "uyumsoft_inbox_invoices");

            migrationBuilder.DropColumn(
                name: "tax_total",
                table: "uyumsoft_inbox_invoices");
        }

        private bool IsSqlServer() =>
            ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);
    }
}
