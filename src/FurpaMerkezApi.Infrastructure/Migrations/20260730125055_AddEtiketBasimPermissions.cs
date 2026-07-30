using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEtiketBasimPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("5c555522-27d5-ab96-b9f0-4c933b86c12e"), "kasa-islemleri.etiket-basim.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Detay yetkisi.", "EtiketBasim Detay", null },
                    { new Guid("8f2efb39-38c7-10d2-5574-427d56a53d7d"), "kasa-islemleri.etiket-basim.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Listele yetkisi.", "EtiketBasim Listele", null },
                    { new Guid("b0e99a09-1761-8c46-13cb-5f6f37a75374"), "kasa-islemleri.etiket-basim.transfer", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Aktar yetkisi.", "EtiketBasim Aktar", null },
                    { new Guid("b2e146a3-7b64-c1ae-9a75-d0faac25258e"), "kasa-islemleri.etiket-basim.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Guncelle yetkisi.", "EtiketBasim Guncelle", null },
                    { new Guid("ce902d4b-1a05-304e-3cef-6b3e864039a6"), "kasa-islemleri.etiket-basim.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Tum Depolar yetkisi.", "EtiketBasim Tum Depolar", null },
                    { new Guid("e06804dc-485d-0d77-bbab-41e3ff9d02ba"), "kasa-islemleri.etiket-basim.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Ekle yetkisi.", "EtiketBasim Ekle", null },
                    { new Guid("e7ad6a0c-f4ef-620f-0972-862663f2ab09"), "kasa-islemleri.etiket-basim.delete", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > EtiketBasim > Sil yetkisi.", "EtiketBasim Sil", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("5c555522-27d5-ab96-b9f0-4c933b86c12e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8f2efb39-38c7-10d2-5574-427d56a53d7d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b0e99a09-1761-8c46-13cb-5f6f37a75374"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b2e146a3-7b64-c1ae-9a75-d0faac25258e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ce902d4b-1a05-304e-3cef-6b3e864039a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e06804dc-485d-0d77-bbab-41e3ff9d02ba"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e7ad6a0c-f4ef-620f-0972-862663f2ab09"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5c555522-27d5-ab96-b9f0-4c933b86c12e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8f2efb39-38c7-10d2-5574-427d56a53d7d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b0e99a09-1761-8c46-13cb-5f6f37a75374"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b2e146a3-7b64-c1ae-9a75-d0faac25258e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ce902d4b-1a05-304e-3cef-6b3e864039a6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e06804dc-485d-0d77-bbab-41e3ff9d02ba"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e7ad6a0c-f4ef-620f-0972-862663f2ab09"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5c555522-27d5-ab96-b9f0-4c933b86c12e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8f2efb39-38c7-10d2-5574-427d56a53d7d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b0e99a09-1761-8c46-13cb-5f6f37a75374"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b2e146a3-7b64-c1ae-9a75-d0faac25258e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ce902d4b-1a05-304e-3cef-6b3e864039a6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e06804dc-485d-0d77-bbab-41e3ff9d02ba"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e7ad6a0c-f4ef-620f-0972-862663f2ab09"));
        }
    }
}
