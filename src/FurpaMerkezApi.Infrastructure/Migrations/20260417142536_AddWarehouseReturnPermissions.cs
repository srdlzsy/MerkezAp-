using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseReturnPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0a9b6883-835d-5b63-9d0e-12ba6aa6c1c5"), "iade-islemleri.depo-iadeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > DepoIadeleri > Guncelle yetkisi.", "DepoIadeleri Guncelle", null },
                    { new Guid("8c02a258-1789-6e18-333d-7bdce13ce875"), "iade-islemleri.depo-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > DepoIadeleri > Listele yetkisi.", "DepoIadeleri Listele", null },
                    { new Guid("91514984-25b2-32ca-2312-204936eac815"), "iade-islemleri.depo-iadeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > DepoIadeleri > Ekle yetkisi.", "DepoIadeleri Ekle", null },
                    { new Guid("a6a83d22-4fc5-b7d3-3f29-12f67a5b498a"), "iade-islemleri.depo-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > DepoIadeleri > Detay yetkisi.", "DepoIadeleri Detay", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0a9b6883-835d-5b63-9d0e-12ba6aa6c1c5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8c02a258-1789-6e18-333d-7bdce13ce875"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("91514984-25b2-32ca-2312-204936eac815"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a6a83d22-4fc5-b7d3-3f29-12f67a5b498a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0a9b6883-835d-5b63-9d0e-12ba6aa6c1c5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8c02a258-1789-6e18-333d-7bdce13ce875"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("91514984-25b2-32ca-2312-204936eac815"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a6a83d22-4fc5-b7d3-3f29-12f67a5b498a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0a9b6883-835d-5b63-9d0e-12ba6aa6c1c5"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c02a258-1789-6e18-333d-7bdce13ce875"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("91514984-25b2-32ca-2312-204936eac815"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a6a83d22-4fc5-b7d3-3f29-12f67a5b498a"));
        }
    }
}
