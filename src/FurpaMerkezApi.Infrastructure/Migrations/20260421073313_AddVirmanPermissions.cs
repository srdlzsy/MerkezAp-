using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVirmanPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("1edc9807-8b0e-43b7-c9f7-e50d7c3e08b0"), "stok-islemleri.virmanlar.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > Virmanlar > Ekle yetkisi.", "Virmanlar Ekle", null },
                    { new Guid("5a8b68be-cf08-15f8-2cd7-9fa55a37e53b"), "stok-islemleri.virmanlar.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > Virmanlar > Guncelle yetkisi.", "Virmanlar Guncelle", null },
                    { new Guid("8c09ac40-28e5-716b-15be-40fb7cb12965"), "stok-islemleri.virmanlar.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > Virmanlar > Detay yetkisi.", "Virmanlar Detay", null },
                    { new Guid("f8b6b372-4e16-eb5b-6550-d91804233ee6"), "stok-islemleri.virmanlar.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "StokIslemleri > Virmanlar > Listele yetkisi.", "Virmanlar Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("1edc9807-8b0e-43b7-c9f7-e50d7c3e08b0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5a8b68be-cf08-15f8-2cd7-9fa55a37e53b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8c09ac40-28e5-716b-15be-40fb7cb12965"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("f8b6b372-4e16-eb5b-6550-d91804233ee6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1edc9807-8b0e-43b7-c9f7-e50d7c3e08b0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5a8b68be-cf08-15f8-2cd7-9fa55a37e53b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8c09ac40-28e5-716b-15be-40fb7cb12965"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("f8b6b372-4e16-eb5b-6550-d91804233ee6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1edc9807-8b0e-43b7-c9f7-e50d7c3e08b0"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5a8b68be-cf08-15f8-2cd7-9fa55a37e53b"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8c09ac40-28e5-716b-15be-40fb7cb12965"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("f8b6b372-4e16-eb5b-6550-d91804233ee6"));
        }
    }
}
