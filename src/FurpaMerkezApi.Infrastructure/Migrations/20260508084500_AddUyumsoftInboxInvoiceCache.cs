using System;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260508084500_AddUyumsoftInboxInvoiceCache")]
    public partial class AddUyumsoftInboxInvoiceCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "uyumsoft_inbox_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    invoice_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    service_document_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    local_document_id = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    customer_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    customer_tckn_vkn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    create_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    invoice_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    invoice_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    invoice_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    despatch_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false),
                    is_printed = table.Column<bool>(type: "boolean", nullable: false),
                    is_standard = table.Column<bool>(type: "boolean", nullable: false),
                    status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    envelope_status_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_synchronized_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uyumsoft_inbox_invoices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_uyumsoft_inbox_invoices_invoice_date",
                table: "uyumsoft_inbox_invoices",
                column: "invoice_date");

            migrationBuilder.CreateIndex(
                name: "ix_uyumsoft_inbox_invoices_processed_printed",
                table: "uyumsoft_inbox_invoices",
                columns: new[] { "is_processed", "is_printed" });

            migrationBuilder.CreateIndex(
                name: "ux_uyumsoft_inbox_invoices_document_id",
                table: "uyumsoft_inbox_invoices",
                column: "document_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "uyumsoft_inbox_invoices");
        }
    }
}
