using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserClientRoleMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_user_client_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    client_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    role_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    assigned_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user_client_roles", x => new { x.user_id, x.client_type, x.role_id });
                    table.ForeignKey(
                        name: "FK_app_user_client_roles_app_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "app_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_app_user_client_roles_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_client_roles_role_id",
                table: "app_user_client_roles",
                column: "role_id");

            migrationBuilder.Sql(
                """
                DECLARE @WarehouseUserRoleId uniqueidentifier = '761efe81-309f-4f52-9752-5f184657a1d8';
                DECLARE @MagazaciRoleId uniqueidentifier = '2d5f7156-a332-497a-ba63-6194e56df746';
                DECLARE @TerminalRoleId uniqueidentifier = '3c1daafe-5922-466e-9f79-6d2ca34ce84d';
                DECLARE @AssignedAtUtc datetime2 = SYSUTCDATETIME();

                INSERT INTO app_user_client_roles (user_id, client_type, role_id, assigned_at_utc)
                SELECT warehouse_user.user_id, N'terminal', @TerminalRoleId, @AssignedAtUtc
                FROM app_user_roles AS warehouse_user
                INNER JOIN app_roles AS terminal_role
                    ON terminal_role.id = @TerminalRoleId
                    AND terminal_role.is_active = 1
                WHERE warehouse_user.role_id = @WarehouseUserRoleId;

                INSERT INTO app_user_client_roles (user_id, client_type, role_id, assigned_at_utc)
                SELECT warehouse_user.user_id, N'web', assigned_role.role_id, @AssignedAtUtc
                FROM app_user_roles AS warehouse_user
                INNER JOIN app_user_roles AS assigned_role
                    ON assigned_role.user_id = warehouse_user.user_id
                    AND assigned_role.role_id NOT IN (@WarehouseUserRoleId, @TerminalRoleId)
                INNER JOIN app_roles AS role
                    ON role.id = assigned_role.role_id
                    AND role.is_active = 1
                WHERE warehouse_user.role_id = @WarehouseUserRoleId;

                INSERT INTO app_user_client_roles (user_id, client_type, role_id, assigned_at_utc)
                SELECT warehouse_user.user_id, N'web', @MagazaciRoleId, @AssignedAtUtc
                FROM app_user_roles AS warehouse_user
                INNER JOIN app_roles AS magazaci_role
                    ON magazaci_role.id = @MagazaciRoleId
                    AND magazaci_role.is_active = 1
                WHERE warehouse_user.role_id = @WarehouseUserRoleId
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM app_user_client_roles AS existing
                      WHERE existing.user_id = warehouse_user.user_id
                        AND existing.client_type = N'web'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_user_client_roles");
        }
    }
}
