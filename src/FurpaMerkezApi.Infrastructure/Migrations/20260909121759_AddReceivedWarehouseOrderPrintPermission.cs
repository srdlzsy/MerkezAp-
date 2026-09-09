using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivedWarehouseOrderPrintPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[] { new Guid("7173d75f-4116-5f83-cac6-9ebde3b46c24"), "siparis-islemleri.alinan-depo-siparisleri.print", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SiparisIslemleri > AlinanDepoSiparisleri > Yazdir yetkisi.", "AlinanDepoSiparisleri Yazdir", null });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[] { new Guid("7173d75f-4116-5f83-cac6-9ebde3b46c24"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7173d75f-4116-5f83-cac6-9ebde3b46c24"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7173d75f-4116-5f83-cac6-9ebde3b46c24"));
        }
    }
}
