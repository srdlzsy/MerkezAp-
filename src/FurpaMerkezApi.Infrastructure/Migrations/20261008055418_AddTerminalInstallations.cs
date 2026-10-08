using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTerminalInstallations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "terminal_installations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    app_version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    build_number = table.Column<int>(type: "int", nullable: false),
                    warehouse_no = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    device_model = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    android_version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    android_sdk = table.Column<int>(type: "int", nullable: true),
                    supported_abis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    first_seen_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    last_ip_address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    previous_warehouse_no = table.Column<int>(type: "int", nullable: true),
                    warehouse_changed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    warehouse_change_count = table.Column<int>(type: "int", nullable: false),
                    version_changed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminal_installations", x => x.id);
                    table.ForeignKey(
                        name: "FK_terminal_installations_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "app_permissions",
                columns: new[] { "id", "code", "created_at_utc", "description", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("2aa9e982-fcce-9ad9-70be-0e5e7d4be965"), "ayar-islemleri.terminal-cihazlari.list", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > TerminalCihazlari > Listele yetkisi.", "TerminalCihazlari Listele", null },
                    { new Guid("bc570163-1f8f-e2eb-4ee9-c2bcdcdc1f01"), "ayar-islemleri.terminal-cihazlari.manage", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > TerminalCihazlari > Yonet yetkisi.", "TerminalCihazlari Yonet", null },
                    { new Guid("c93b5839-59e9-2683-3a6e-4b264ce8294d"), "ayar-islemleri.terminal-cihazlari.detail", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > TerminalCihazlari > Detay yetkisi.", "TerminalCihazlari Detay", null },
                    { new Guid("cfc11395-83dc-4430-4695-a3aef6074207"), "ayar-islemleri.terminal-cihazlari.all-warehouses", new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "AyarIslemleri > TerminalCihazlari > Tum Depolar yetkisi.", "TerminalCihazlari Tum Depolar", null }
                });

            migrationBuilder.InsertData(
                table: "app_role_permissions",
                columns: new[] { "permission_id", "role_id", "assigned_at_utc" },
                values: new object[,]
                {
                    { new Guid("2aa9e982-fcce-9ad9-70be-0e5e7d4be965"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("bc570163-1f8f-e2eb-4ee9-c2bcdcdc1f01"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("c93b5839-59e9-2683-3a6e-4b264ce8294d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("cfc11395-83dc-4430-4695-a3aef6074207"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_terminal_installations_user_id",
                table: "terminal_installations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_terminal_installations_build_last_seen",
                table: "terminal_installations",
                columns: new[] { "build_number", "last_seen_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_terminal_installations_warehouse_last_seen",
                table: "terminal_installations",
                columns: new[] { "warehouse_no", "last_seen_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_terminal_installations_device_id",
                table: "terminal_installations",
                column: "device_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "terminal_installations");

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("2aa9e982-fcce-9ad9-70be-0e5e7d4be965"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("bc570163-1f8f-e2eb-4ee9-c2bcdcdc1f01"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("c93b5839-59e9-2683-3a6e-4b264ce8294d"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("cfc11395-83dc-4430-4695-a3aef6074207"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("2aa9e982-fcce-9ad9-70be-0e5e7d4be965"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("bc570163-1f8f-e2eb-4ee9-c2bcdcdc1f01"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("c93b5839-59e9-2683-3a6e-4b264ce8294d"));

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("cfc11395-83dc-4430-4695-a3aef6074207"));
        }
    }
}
