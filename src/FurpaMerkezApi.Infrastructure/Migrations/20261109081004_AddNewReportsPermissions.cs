using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewReportsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("229fe8ac-88b7-7d5d-6166-24f010ae0cbb"), "rapor-islemleri.mal-kabul-raporlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > MalKabulRaporlari > Listele yetkisi.", "MalKabulRaporlari Listele", null },
                    { new Guid("4aae24f1-ef52-3834-7fe0-e60526f369a3"), "rapor-islemleri.ciro-hedef-raporlari.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > CiroHedefRaporlari > Tum Depolar yetkisi.", "CiroHedefRaporlari Tum Depolar", null },
                    { new Guid("4acc2e2c-be8e-e93c-83f1-0e03de05cdd1"), "rapor-islemleri.kasa-raporlari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > KasaRaporlari > Sayfa yetkisi.", "KasaRaporlari Sayfa", null },
                    { new Guid("4de5f06d-e8f4-7e4f-619e-5bdc04ef44ea"), "rapor-islemleri.ciro-hedef-raporlari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > CiroHedefRaporlari > Sayfa yetkisi.", "CiroHedefRaporlari Sayfa", null },
                    { new Guid("581e8299-f4ef-7685-7f08-c0dd2c8e6140"), "rapor-islemleri.mal-kabul-raporlari.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > MalKabulRaporlari > Tum Depolar yetkisi.", "MalKabulRaporlari Tum Depolar", null },
                    { new Guid("6435df0a-444f-1de5-a671-dcc4c9a28c7d"), "rapor-islemleri.kasa-raporlari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > KasaRaporlari > Detay yetkisi.", "KasaRaporlari Detay", null },
                    { new Guid("678484ea-3486-0ea5-5423-2cac62f4f885"), "rapor-islemleri.ciro-hedef-raporlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > CiroHedefRaporlari > Listele yetkisi.", "CiroHedefRaporlari Listele", null },
                    { new Guid("73d9418b-f6c4-492d-3197-7a63c653c078"), "rapor-islemleri.kasa-raporlari.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > KasaRaporlari > Tum Depolar yetkisi.", "KasaRaporlari Tum Depolar", null },
                    { new Guid("79197b25-da31-deaf-de32-5ce34569c5a8"), "rapor-islemleri.mal-kabul-raporlari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > MalKabulRaporlari > Detay yetkisi.", "MalKabulRaporlari Detay", null },
                    { new Guid("ebfdf730-4d58-c2e7-fefa-6713ad8d11c2"), "rapor-islemleri.kasa-raporlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > KasaRaporlari > Listele yetkisi.", "KasaRaporlari Listele", null },
                    { new Guid("f4a3350d-3804-fbad-7046-584e2de1a39e"), "rapor-islemleri.mal-kabul-raporlari.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "RaporIslemleri > MalKabulRaporlari > Sayfa yetkisi.", "MalKabulRaporlari Sayfa", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("229fe8ac-88b7-7d5d-6166-24f010ae0cbb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4aae24f1-ef52-3834-7fe0-e60526f369a3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4acc2e2c-be8e-e93c-83f1-0e03de05cdd1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("4de5f06d-e8f4-7e4f-619e-5bdc04ef44ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("581e8299-f4ef-7685-7f08-c0dd2c8e6140"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6435df0a-444f-1de5-a671-dcc4c9a28c7d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("678484ea-3486-0ea5-5423-2cac62f4f885"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("73d9418b-f6c4-492d-3197-7a63c653c078"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("79197b25-da31-deaf-de32-5ce34569c5a8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("ebfdf730-4d58-c2e7-fefa-6713ad8d11c2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f4a3350d-3804-fbad-7046-584e2de1a39e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("229fe8ac-88b7-7d5d-6166-24f010ae0cbb"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4aae24f1-ef52-3834-7fe0-e60526f369a3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4acc2e2c-be8e-e93c-83f1-0e03de05cdd1"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4de5f06d-e8f4-7e4f-619e-5bdc04ef44ea"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("581e8299-f4ef-7685-7f08-c0dd2c8e6140"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6435df0a-444f-1de5-a671-dcc4c9a28c7d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("678484ea-3486-0ea5-5423-2cac62f4f885"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("73d9418b-f6c4-492d-3197-7a63c653c078"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("79197b25-da31-deaf-de32-5ce34569c5a8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("ebfdf730-4d58-c2e7-fefa-6713ad8d11c2"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f4a3350d-3804-fbad-7046-584e2de1a39e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("229fe8ac-88b7-7d5d-6166-24f010ae0cbb"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4aae24f1-ef52-3834-7fe0-e60526f369a3"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4acc2e2c-be8e-e93c-83f1-0e03de05cdd1"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4de5f06d-e8f4-7e4f-619e-5bdc04ef44ea"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("581e8299-f4ef-7685-7f08-c0dd2c8e6140"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6435df0a-444f-1de5-a671-dcc4c9a28c7d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("678484ea-3486-0ea5-5423-2cac62f4f885"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("73d9418b-f6c4-492d-3197-7a63c653c078"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("79197b25-da31-deaf-de32-5ce34569c5a8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("ebfdf730-4d58-c2e7-fefa-6713ad8d11c2"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f4a3350d-3804-fbad-7046-584e2de1a39e"));
        }
    }
}
