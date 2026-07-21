using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAndPromotionReportPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("8cae42af-959b-d606-d98d-ed9b7809ce0b"), "rapor-islemleri.stok-raporlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > StokRaporlari > Listele yetkisi.", "StokRaporlari Listele", null },
                    { new Guid("b6df0051-cd4b-f115-23e3-dabe0a158a8b"), "rapor-islemleri.promosyon-raporlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > PromosyonRaporlari > Listele yetkisi.", "PromosyonRaporlari Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("8cae42af-959b-d606-d98d-ed9b7809ce0b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b6df0051-cd4b-f115-23e3-dabe0a158a8b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8cae42af-959b-d606-d98d-ed9b7809ce0b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b6df0051-cd4b-f115-23e3-dabe0a158a8b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8cae42af-959b-d606-d98d-ed9b7809ce0b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b6df0051-cd4b-f115-23e3-dabe0a158a8b"));
        }
    }
}
