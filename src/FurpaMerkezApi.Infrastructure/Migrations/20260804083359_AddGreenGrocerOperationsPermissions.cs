using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGreenGrocerOperationsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("4c1d56b3-8118-1b26-61bc-3d7a14929c56"), "green-grocer.operations.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > Operations > Tum Depolar yetkisi.", "Operations Tum Depolar", null },
                    { new Guid("5de21f6b-8f62-7572-e757-24d3496082d7"), "green-grocer.operations.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > Operations > Listele yetkisi.", "Operations Listele", null },
                    { new Guid("6ff0fa91-1ee1-007a-155e-302ab904eebf"), "green-grocer.operations.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > Operations > Sayfa yetkisi.", "Operations Sayfa", null },
                    { new Guid("cc67c409-9264-efcf-0e23-3fa96cb40d73"), "green-grocer.operations.create", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "GreenGrocer > Operations > Ekle yetkisi.", "Operations Ekle", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("4c1d56b3-8118-1b26-61bc-3d7a14929c56"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("5de21f6b-8f62-7572-e757-24d3496082d7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("6ff0fa91-1ee1-007a-155e-302ab904eebf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("cc67c409-9264-efcf-0e23-3fa96cb40d73"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("4c1d56b3-8118-1b26-61bc-3d7a14929c56"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("5de21f6b-8f62-7572-e757-24d3496082d7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("6ff0fa91-1ee1-007a-155e-302ab904eebf"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("cc67c409-9264-efcf-0e23-3fa96cb40d73"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("4c1d56b3-8118-1b26-61bc-3d7a14929c56"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("5de21f6b-8f62-7572-e757-24d3496082d7"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("6ff0fa91-1ee1-007a-155e-302ab904eebf"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("cc67c409-9264-efcf-0e23-3fa96cb40d73"));
        }
    }
}
