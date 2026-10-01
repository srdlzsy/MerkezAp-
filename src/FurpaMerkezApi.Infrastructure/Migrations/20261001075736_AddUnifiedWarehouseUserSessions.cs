using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedWarehouseUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_type",
                table: "app_refresh_tokens",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "web");

            migrationBuilder.AddColumn<string>(
                name: "device_id",
                table: "app_refresh_tokens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "login_ip_address",
                table: "app_refresh_tokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "warehouse_no",
                table: "app_refresh_tokens",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "0");

            migrationBuilder.InsertData(
                table: "app_roles",
                columns: new[] { "id", "created_at_utc", "description", "is_active", "name", "updated_at_utc" },
                values: new object[] { new Guid("761efe81-309f-4f52-9752-5f184657a1d8"), new DateTime(2026, 4, 14, 0, 0, 0, 0, DateTimeKind.Utc), "Web ve terminal oturumlarinda kullanilan ortak sube rolu.", true, "SubeKullanicisi", null });

            migrationBuilder.Sql(
                """
                UPDATE token
                SET token.warehouse_no = [user].warehouse_no,
                    token.client_type = CASE
                        WHEN terminal_user.user_id IS NOT NULL THEN N'terminal'
                        ELSE N'web'
                    END
                FROM app_refresh_tokens AS token
                INNER JOIN app_users AS [user] ON [user].id = token.user_id
                LEFT JOIN app_user_roles AS terminal_user
                    ON terminal_user.user_id = token.user_id
                    AND terminal_user.role_id = '3c1daafe-5922-466e-9f79-6d2ca34ce84d';

                INSERT INTO app_role_permissions (role_id, permission_id, assigned_at_utc)
                SELECT
                    '761efe81-309f-4f52-9752-5f184657a1d8',
                    source.permission_id,
                    SYSUTCDATETIME()
                FROM
                (
                    SELECT DISTINCT permission_id
                    FROM app_role_permissions
                    WHERE role_id IN
                    (
                        '2d5f7156-a332-497a-ba63-6194e56df746',
                        '3c1daafe-5922-466e-9f79-6d2ca34ce84d'
                    )
                ) AS source
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM app_role_permissions AS existing
                    WHERE existing.role_id = '761efe81-309f-4f52-9752-5f184657a1d8'
                      AND existing.permission_id = source.permission_id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_roles",
                keyColumn: "id",
                keyValue: new Guid("761efe81-309f-4f52-9752-5f184657a1d8"));

            migrationBuilder.DropColumn(
                name: "client_type",
                table: "app_refresh_tokens");

            migrationBuilder.DropColumn(
                name: "device_id",
                table: "app_refresh_tokens");

            migrationBuilder.DropColumn(
                name: "login_ip_address",
                table: "app_refresh_tokens");

            migrationBuilder.DropColumn(
                name: "warehouse_no",
                table: "app_refresh_tokens");
        }
    }
}
