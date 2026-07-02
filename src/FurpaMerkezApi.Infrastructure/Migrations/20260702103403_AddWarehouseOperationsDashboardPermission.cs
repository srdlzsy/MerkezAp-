using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseOperationsDashboardPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions]
                    WHERE [code] = N'operasyon-islemleri.depo-operasyon-paneli.list'
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions]
                    WHERE [id] = '7092498f-5685-fb29-f81e-10d422f4a5b8'
                )
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('7092498f-5685-fb29-f81e-10d422f4a5b8', N'operasyon-islemleri.depo-operasyon-paneli.list', '2026-04-14T00:00:00.0000000Z', N'OperasyonIslemleri > DepoOperasyonPaneli > Listele yetkisi.', N'DepoOperasyonPaneli Listele', NULL);
                END

                DECLARE @DashboardPermissionId uniqueidentifier;

                SELECT @DashboardPermissionId = [id]
                FROM [app_permissions]
                WHERE [code] = N'operasyon-islemleri.depo-operasyon-paneli.list';

                IF @DashboardPermissionId IS NOT NULL
                AND EXISTS (
                    SELECT 1
                    FROM [app_roles]
                    WHERE [id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                )
                AND NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions]
                    WHERE [permission_id] = @DashboardPermissionId
                      AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                )
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES (@DashboardPermissionId, '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', '2026-04-14T00:00:00.0000000Z');
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("7092498f-5685-fb29-f81e-10d422f4a5b8"), new Guid("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a") });

            migrationBuilder.DeleteData(
                table: "app_permissions",
                keyColumn: "id",
                keyValue: new Guid("7092498f-5685-fb29-f81e-10d422f4a5b8"));
        }
    }
}
