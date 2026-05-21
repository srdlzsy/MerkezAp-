using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchLookupPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("7b02acb4-e7c8-bfba-0d52-37286ac295d0"), "arama-islemleri.fiyat-gor.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AramaIslemleri > FiyatGor > Listele yetkisi.", "FiyatGor Listele", null },
                    { new Guid("a59282d8-98e1-73f7-cb33-cd72cb17afb9"), "arama-islemleri.cari-bul.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AramaIslemleri > CariBul > Listele yetkisi.", "CariBul Listele", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("7b02acb4-e7c8-bfba-0d52-37286ac295d0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a59282d8-98e1-73f7-cb33-cd72cb17afb9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7b02acb4-e7c8-bfba-0d52-37286ac295d0"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("a59282d8-98e1-73f7-cb33-cd72cb17afb9"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7b02acb4-e7c8-bfba-0d52-37286ac295d0"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("a59282d8-98e1-73f7-cb33-cd72cb17afb9"));
        }
    }
}
