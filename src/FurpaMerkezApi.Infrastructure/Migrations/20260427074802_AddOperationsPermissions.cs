using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("6b6947e2-6d4c-be49-209d-df7e2d729376"), "operasyon-islemleri.operations.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > Operations > Ekle yetkisi.", "Operations Ekle", null },
                    { new Guid("aa6b811d-ebe3-77da-817a-6c6a0db4807d"), "operasyon-islemleri.operations.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > Operations > Listele yetkisi.", "Operations Listele", null },
                    { new Guid("aa824ba7-40df-cd78-4c6d-cc7f0662dfd6"), "operasyon-islemleri.operations.update", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > Operations > Guncelle yetkisi.", "Operations Guncelle", null },
                    { new Guid("e562f7cc-2e91-f49e-b80f-200acb23acce"), "operasyon-islemleri.operations.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > Operations > Detay yetkisi.", "Operations Detay", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("6b6947e2-6d4c-be49-209d-df7e2d729376"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("aa6b811d-ebe3-77da-817a-6c6a0db4807d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("aa824ba7-40df-cd78-4c6d-cc7f0662dfd6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("e562f7cc-2e91-f49e-b80f-200acb23acce"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6b6947e2-6d4c-be49-209d-df7e2d729376"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("aa6b811d-ebe3-77da-817a-6c6a0db4807d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("aa824ba7-40df-cd78-4c6d-cc7f0662dfd6"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("e562f7cc-2e91-f49e-b80f-200acb23acce"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6b6947e2-6d4c-be49-209d-df7e2d729376"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("aa6b811d-ebe3-77da-817a-6c6a0db4807d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("aa824ba7-40df-cd78-4c6d-cc7f0662dfd6"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("e562f7cc-2e91-f49e-b80f-200acb23acce"));
        }
    }
}
