using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVarYokSearchPermissions : Migration
    {
        private const string AdministratorRoleId = "2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a";
        private const string PagePermissionId = "c4772b76-3666-2fe8-e657-cb1659d04dab";
        private const string ListPermissionId = "3a2e8b7c-1fd0-f9bb-0354-159f597790f3";
        private const string AllWarehousesPermissionId = "1ab60e4a-2a62-11d9-5520-e6104697a0d8";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'arama-islemleri.var-yok.page')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [name], [description], [created_at_utc], [updated_at_utc])
                    VALUES ('{PagePermissionId}', N'arama-islemleri.var-yok.page', N'VarYok Sayfa', N'AramaIslemleri > VarYok > Sayfa yetkisi.', '2026-04-14T00:00:00.0000000Z', NULL)
                END

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'arama-islemleri.var-yok.list')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [name], [description], [created_at_utc], [updated_at_utc])
                    VALUES ('{ListPermissionId}', N'arama-islemleri.var-yok.list', N'VarYok Listele', N'AramaIslemleri > VarYok > Listele yetkisi.', '2026-04-14T00:00:00.0000000Z', NULL)
                END

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'arama-islemleri.var-yok.all-warehouses')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [name], [description], [created_at_utc], [updated_at_utc])
                    VALUES ('{AllWarehousesPermissionId}', N'arama-islemleri.var-yok.all-warehouses', N'VarYok Tum Depolar', N'AramaIslemleri > VarYok > Tum Depolar yetkisi.', '2026-04-14T00:00:00.0000000Z', NULL)
                END

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT permission_id, '{AdministratorRoleId}', '2026-04-14T00:00:00.0000000Z'
                FROM (VALUES
                    ('{PagePermissionId}'),
                    ('{ListPermissionId}'),
                    ('{AllWarehousesPermissionId}')
                ) AS permissions(permission_id)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS rolePermission
                    WHERE rolePermission.[permission_id] = permissions.permission_id
                      AND rolePermission.[role_id] = '{AdministratorRoleId}'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DELETE FROM [app_role_permissions]
                WHERE [permission_id] IN ('{PagePermissionId}', '{ListPermissionId}', '{AllWarehousesPermissionId}');

                DELETE FROM [app_permissions]
                WHERE [id] IN ('{PagePermissionId}', '{ListPermissionId}', '{AllWarehousesPermissionId}')
                   OR [code] IN (
                        N'arama-islemleri.var-yok.page',
                        N'arama-islemleri.var-yok.list',
                        N'arama-islemleri.var-yok.all-warehouses'
                   );
                """);
        }
    }
}
