using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryCountPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("02bea4f4-47f6-2689-3a45-e0e52d233770"), "stok-islemleri.sayim-sonuclari.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > SayimSonuclari > Guncelle yetkisi.", "SayimSonuclari Guncelle", null },
                    { new Guid("0b159db8-a882-b035-5db1-5e737c0992ce"), "stok-islemleri.sayim-sonuclari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > SayimSonuclari > Listele yetkisi.", "SayimSonuclari Listele", null },
                    { new Guid("6d5a41e3-132e-691b-ae6d-be982b5d86e4"), "stok-islemleri.sayim-sonuclari.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > SayimSonuclari > Ekle yetkisi.", "SayimSonuclari Ekle", null },
                    { new Guid("976a0d95-c590-83c3-9c72-72c90da2a464"), "stok-islemleri.sayim-sonuclari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > SayimSonuclari > Detay yetkisi.", "SayimSonuclari Detay", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("02bea4f4-47f6-2689-3a45-e0e52d233770"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0b159db8-a882-b035-5db1-5e737c0992ce"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6d5a41e3-132e-691b-ae6d-be982b5d86e4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("976a0d95-c590-83c3-9c72-72c90da2a464"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("02bea4f4-47f6-2689-3a45-e0e52d233770"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0b159db8-a882-b035-5db1-5e737c0992ce"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6d5a41e3-132e-691b-ae6d-be982b5d86e4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("976a0d95-c590-83c3-9c72-72c90da2a464"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("02bea4f4-47f6-2689-3a45-e0e52d233770"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0b159db8-a882-b035-5db1-5e737c0992ce"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6d5a41e3-132e-691b-ae6d-be982b5d86e4"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("976a0d95-c590-83c3-9c72-72c90da2a464"));
        }
    }
}
