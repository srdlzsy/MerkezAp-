using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrendyolGoIntegrationPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("1fbf9e40-d211-26ba-dc9e-4b162a2b758c"), "entegrasyon-islemleri.trendyol-go.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > TrendyolGo > Sayfa yetkisi.", "TrendyolGo Sayfa", null },
                    { new Guid("8af7c4b4-f27d-ccb4-88dc-3668b09b709a"), "entegrasyon-islemleri.trendyol-go.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > TrendyolGo > Guncelle yetkisi.", "TrendyolGo Guncelle", null },
                    { new Guid("9ffcf087-2f26-584c-4d92-e4abf2655bf5"), "entegrasyon-islemleri.trendyol-go.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > TrendyolGo > Detay yetkisi.", "TrendyolGo Detay", null },
                    { new Guid("eff6075a-bfb4-5e21-a409-93bacf8eba9b"), "entegrasyon-islemleri.trendyol-go.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "EntegrasyonIslemleri > TrendyolGo > Listele yetkisi.", "TrendyolGo Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("1fbf9e40-d211-26ba-dc9e-4b162a2b758c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("8af7c4b4-f27d-ccb4-88dc-3668b09b709a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9ffcf087-2f26-584c-4d92-e4abf2655bf5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("eff6075a-bfb4-5e21-a409-93bacf8eba9b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("1fbf9e40-d211-26ba-dc9e-4b162a2b758c"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("8af7c4b4-f27d-ccb4-88dc-3668b09b709a"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("9ffcf087-2f26-584c-4d92-e4abf2655bf5"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("eff6075a-bfb4-5e21-a409-93bacf8eba9b"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("1fbf9e40-d211-26ba-dc9e-4b162a2b758c"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("8af7c4b4-f27d-ccb4-88dc-3668b09b709a"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("9ffcf087-2f26-584c-4d92-e4abf2655bf5"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("eff6075a-bfb4-5e21-a409-93bacf8eba9b"));
        }
    }
}
