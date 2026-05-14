using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyShipmentAndReturnPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0d7dcfaa-7fa9-f498-82dc-14a65b04b56e"), "iade-islemleri.firma-iadeleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > FirmaIadeleri > Detay yetkisi.", "FirmaIadeleri Detay", null },
                    { new Guid("218c674f-77bb-027c-9e68-dfbf9e2f6cf0"), "sevk-islemleri.firma-sevkleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > FirmaSevkleri > Ekle yetkisi.", "FirmaSevkleri Ekle", null },
                    { new Guid("2cb773b0-fb98-357e-103a-d4f1c6b62f78"), "sevk-islemleri.firma-sevkleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > FirmaSevkleri > Listele yetkisi.", "FirmaSevkleri Listele", null },
                    { new Guid("31b8a318-b257-4a55-6cfd-817114486445"), "iade-islemleri.firma-iadeleri.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > FirmaIadeleri > Ekle yetkisi.", "FirmaIadeleri Ekle", null },
                    { new Guid("7933b9ad-9225-22ad-6003-f91158f0cd95"), "sevk-islemleri.firma-sevkleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > FirmaSevkleri > Guncelle yetkisi.", "FirmaSevkleri Guncelle", null },
                    { new Guid("90a43d70-aa52-a61c-45aa-a07defb35d1a"), "sevk-islemleri.firma-sevkleri.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "SevkIslemleri > FirmaSevkleri > Detay yetkisi.", "FirmaSevkleri Detay", null },
                    { new Guid("a31529a2-aece-96f0-b8c2-6a62542d6952"), "iade-islemleri.firma-iadeleri.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > FirmaIadeleri > Listele yetkisi.", "FirmaIadeleri Listele", null },
                    { new Guid("c137720a-6a3d-04e8-64a8-56bfb278fec0"), "iade-islemleri.firma-iadeleri.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "IadeIslemleri > FirmaIadeleri > Guncelle yetkisi.", "FirmaIadeleri Guncelle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0d7dcfaa-7fa9-f498-82dc-14a65b04b56e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("218c674f-77bb-027c-9e68-dfbf9e2f6cf0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("2cb773b0-fb98-357e-103a-d4f1c6b62f78"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("31b8a318-b257-4a55-6cfd-817114486445"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("7933b9ad-9225-22ad-6003-f91158f0cd95"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("90a43d70-aa52-a61c-45aa-a07defb35d1a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a31529a2-aece-96f0-b8c2-6a62542d6952"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c137720a-6a3d-04e8-64a8-56bfb278fec0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0d7dcfaa-7fa9-f498-82dc-14a65b04b56e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("218c674f-77bb-027c-9e68-dfbf9e2f6cf0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2cb773b0-fb98-357e-103a-d4f1c6b62f78"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("31b8a318-b257-4a55-6cfd-817114486445"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7933b9ad-9225-22ad-6003-f91158f0cd95"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("90a43d70-aa52-a61c-45aa-a07defb35d1a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a31529a2-aece-96f0-b8c2-6a62542d6952"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c137720a-6a3d-04e8-64a8-56bfb278fec0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0d7dcfaa-7fa9-f498-82dc-14a65b04b56e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("218c674f-77bb-027c-9e68-dfbf9e2f6cf0"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2cb773b0-fb98-357e-103a-d4f1c6b62f78"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("31b8a318-b257-4a55-6cfd-817114486445"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7933b9ad-9225-22ad-6003-f91158f0cd95"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("90a43d70-aa52-a61c-45aa-a07defb35d1a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a31529a2-aece-96f0-b8c2-6a62542d6952"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c137720a-6a3d-04e8-64a8-56bfb278fec0"));
        }
    }
}
