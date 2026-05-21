using System;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260520085000_AddWarehouseReceivingDifferencePermissions")]
    public partial class AddWarehouseReceivingDifferencePermissions : Migration
    {
        private static readonly Guid PermissionId = new("31b9c4fd-80bd-7967-11b0-3fccd5adf5e5");
        private static readonly Guid AdministratorRoleId = new("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a");
        private static readonly DateTime SeededAtUtc = new(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[]
                {
                    PermissionId,
                    "mal-kabul-islemleri.mal-kabul-farklari.list",
                    SeededAtUtc,
                    "MalKabulIslemleri > MalKabulFarklari > Listele yetkisi.",
                    "MalKabulFarklari Listele",
                    null
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[] { PermissionId, AdministratorRoleId, SeededAtUtc });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { PermissionId, AdministratorRoleId });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: PermissionId);
        }
    }
}
