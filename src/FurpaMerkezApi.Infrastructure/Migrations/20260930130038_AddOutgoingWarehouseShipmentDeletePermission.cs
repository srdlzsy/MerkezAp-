using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutgoingWarehouseShipmentDeletePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[] { new Guid("b2bfed00-6870-d047-9ba0-934b98cb78c0"), "sevk-islemleri.giden-depolar-arasi-sevkler.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > GidenDepolarArasiSevkler > Sil yetkisi.", "GidenDepolarArasiSevkler Sil", null });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[] { new Guid("b2bfed00-6870-d047-9ba0-934b98cb78c0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b2bfed00-6870-d047-9ba0-934b98cb78c0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b2bfed00-6870-d047-9ba0-934b98cb78c0"));
        }
    }
}
