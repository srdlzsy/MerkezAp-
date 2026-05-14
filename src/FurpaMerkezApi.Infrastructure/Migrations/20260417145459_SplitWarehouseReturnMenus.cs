using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitWarehouseReturnMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("494678b1-5103-be89-ccdc-973a30b7f2e1"), "iade-islemleri.giden-depo-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GidenDepoIadeleri > Listele yetkisi.", "GidenDepoIadeleri Listele", null },
                    { new Guid("7ca05db0-489f-aa58-ae46-07bb9697b475"), "iade-islemleri.gelen-depo-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GelenDepoIadeleri > Detay yetkisi.", "GelenDepoIadeleri Detay", null },
                    { new Guid("7eb22c61-d781-367a-8bb9-eac551a8ff2e"), "iade-islemleri.gelen-depo-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GelenDepoIadeleri > Listele yetkisi.", "GelenDepoIadeleri Listele", null },
                    { new Guid("9fefa935-cb19-650d-6ed0-b9635b628754"), "iade-islemleri.giden-depo-iadeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GidenDepoIadeleri > Guncelle yetkisi.", "GidenDepoIadeleri Guncelle", null },
                    { new Guid("aab7da23-7b7f-f2c9-ef4d-8cde092af6f3"), "iade-islemleri.giden-depo-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GidenDepoIadeleri > Detay yetkisi.", "GidenDepoIadeleri Detay", null },
                    { new Guid("b117d991-8a82-2746-d397-95d5cb0f05fc"), "iade-islemleri.giden-depo-iadeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > GidenDepoIadeleri > Ekle yetkisi.", "GidenDepoIadeleri Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("494678b1-5103-be89-ccdc-973a30b7f2e1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7ca05db0-489f-aa58-ae46-07bb9697b475"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7eb22c61-d781-367a-8bb9-eac551a8ff2e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9fefa935-cb19-650d-6ed0-b9635b628754"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("aab7da23-7b7f-f2c9-ef4d-8cde092af6f3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b117d991-8a82-2746-d397-95d5cb0f05fc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("494678b1-5103-be89-ccdc-973a30b7f2e1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7ca05db0-489f-aa58-ae46-07bb9697b475"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7eb22c61-d781-367a-8bb9-eac551a8ff2e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9fefa935-cb19-650d-6ed0-b9635b628754"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("aab7da23-7b7f-f2c9-ef4d-8cde092af6f3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b117d991-8a82-2746-d397-95d5cb0f05fc"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("494678b1-5103-be89-ccdc-973a30b7f2e1"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7ca05db0-489f-aa58-ae46-07bb9697b475"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7eb22c61-d781-367a-8bb9-eac551a8ff2e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9fefa935-cb19-650d-6ed0-b9635b628754"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("aab7da23-7b7f-f2c9-ef4d-8cde092af6f3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b117d991-8a82-2746-d397-95d5cb0f05fc"));

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
    }
}
