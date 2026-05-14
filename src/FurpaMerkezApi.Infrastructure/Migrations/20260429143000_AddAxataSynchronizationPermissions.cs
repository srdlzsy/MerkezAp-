using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAxataSynchronizationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("3b4db02d-4e47-32b3-542a-4b85795b2976"), "entegrasyon-islemleri.axata-senkronizasyonu.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > AxataSenkronizasyonu > Listele yetkisi.", "AxataSenkronizasyonu Listele", null },
                    { new Guid("b693fc1e-047c-87f5-642d-85474944546e"), "entegrasyon-islemleri.axata-senkronizasyonu.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > AxataSenkronizasyonu > Detay yetkisi.", "AxataSenkronizasyonu Detay", null },
                    { new Guid("a7d4d21f-2730-b3a8-ed48-f8a2b102fad8"), "entegrasyon-islemleri.axata-senkronizasyonu.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > AxataSenkronizasyonu > Ekle yetkisi.", "AxataSenkronizasyonu Ekle", null },
                    { new Guid("bfac5220-29cd-f9d4-77f2-0dbf5d0afde3"), "entegrasyon-islemleri.axata-senkronizasyonu.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > AxataSenkronizasyonu > Guncelle yetkisi.", "AxataSenkronizasyonu Guncelle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("3b4db02d-4e47-32b3-542a-4b85795b2976"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b693fc1e-047c-87f5-642d-85474944546e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a7d4d21f-2730-b3a8-ed48-f8a2b102fad8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("bfac5220-29cd-f9d4-77f2-0dbf5d0afde3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("3b4db02d-4e47-32b3-542a-4b85795b2976"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b693fc1e-047c-87f5-642d-85474944546e"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a7d4d21f-2730-b3a8-ed48-f8a2b102fad8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("bfac5220-29cd-f9d4-77f2-0dbf5d0afde3"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("3b4db02d-4e47-32b3-542a-4b85795b2976"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b693fc1e-047c-87f5-642d-85474944546e"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a7d4d21f-2730-b3a8-ed48-f8a2b102fad8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("bfac5220-29cd-f9d4-77f2-0dbf5d0afde3"));
        }
    }
}
