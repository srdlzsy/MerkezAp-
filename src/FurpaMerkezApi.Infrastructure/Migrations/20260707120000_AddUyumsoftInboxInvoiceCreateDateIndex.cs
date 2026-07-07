using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations;

[DbContext(typeof(AuthDbContext))]
[Migration("20260707120000_AddUyumsoftInboxInvoiceCreateDateIndex")]
public partial class AddUyumsoftInboxInvoiceCreateDateIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_uyumsoft_inbox_invoices_create_date",
            table: "uyumsoft_inbox_invoices",
            column: "create_date");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_uyumsoft_inbox_invoices_create_date",
            table: "uyumsoft_inbox_invoices");
    }
}
