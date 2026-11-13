using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAxataWarehouseCockpitPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0677eec1-930a-b0bf-2535-d9c00be299e7"), "operasyon-islemleri.axata-depo-kokpiti.page", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > AxataDepoKokpiti > Sayfa yetkisi.", "AxataDepoKokpiti Sayfa", null },
                    { new Guid("378c2fe2-bfb4-989e-220e-d2f5d2f69bc8"), "operasyon-islemleri.axata-depo-kokpiti.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > AxataDepoKokpiti > Listele yetkisi.", "AxataDepoKokpiti Listele", null },
                    { new Guid("71669f3b-77c4-38bb-821f-fb0d55172dff"), "operasyon-islemleri.axata-depo-kokpiti.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > AxataDepoKokpiti > Detay yetkisi.", "AxataDepoKokpiti Detay", null },
                    { new Guid("b284c323-7a1b-52c4-e72d-ffd2a0e01104"), "operasyon-islemleri.axata-depo-kokpiti.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "OperasyonIslemleri > AxataDepoKokpiti > Tum Depolar yetkisi.", "AxataDepoKokpiti Tum Depolar", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("0677eec1-930a-b0bf-2535-d9c00be299e7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("378c2fe2-bfb4-989e-220e-d2f5d2f69bc8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("71669f3b-77c4-38bb-821f-fb0d55172dff"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("b284c323-7a1b-52c4-e72d-ffd2a0e01104"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("0677eec1-930a-b0bf-2535-d9c00be299e7"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("378c2fe2-bfb4-989e-220e-d2f5d2f69bc8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("71669f3b-77c4-38bb-821f-fb0d55172dff"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("b284c323-7a1b-52c4-e72d-ffd2a0e01104"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("0677eec1-930a-b0bf-2535-d9c00be299e7"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("378c2fe2-bfb4-989e-220e-d2f5d2f69bc8"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("71669f3b-77c4-38bb-821f-fb0d55172dff"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("b284c323-7a1b-52c4-e72d-ffd2a0e01104"));
        }
    }
}
