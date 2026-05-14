using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKasaCirolariPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("50bf1b79-7e58-e4d4-b877-51e9e2fe0147"), "kasa-islemleri.kasa-cirolari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaCirolari > Detay yetkisi.", "KasaCirolari Detay", null },
                    { new Guid("d2081f93-5baf-f14f-9ea8-aea77e3120e4"), "kasa-islemleri.kasa-cirolari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "KasaIslemleri > KasaCirolari > Listele yetkisi.", "KasaCirolari Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("50bf1b79-7e58-e4d4-b877-51e9e2fe0147"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("d2081f93-5baf-f14f-9ea8-aea77e3120e4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("50bf1b79-7e58-e4d4-b877-51e9e2fe0147"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("d2081f93-5baf-f14f-9ea8-aea77e3120e4"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("50bf1b79-7e58-e4d4-b877-51e9e2fe0147"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("d2081f93-5baf-f14f-9ea8-aea77e3120e4"));
        }
    }
}
