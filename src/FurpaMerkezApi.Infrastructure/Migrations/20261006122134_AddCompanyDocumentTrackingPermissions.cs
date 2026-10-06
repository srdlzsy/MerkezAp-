using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDocumentTrackingPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'operasyon-islemleri.firma-evrak-takibi.page')
                   AND NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = 'bfd06389-093f-5315-f627-c7a062afe536')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('bfd06389-093f-5315-f627-c7a062afe536', N'operasyon-islemleri.firma-evrak-takibi.page', '2026-04-14T00:00:00.0000000Z', N'OperasyonIslemleri > FirmaEvrakTakibi > Sayfa yetkisi.', N'FirmaEvrakTakibi Sayfa', NULL);
                END

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'operasyon-islemleri.firma-evrak-takibi.list')
                   AND NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = 'd1a6bc82-5ccb-422e-dbce-14978a0959f5')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('d1a6bc82-5ccb-422e-dbce-14978a0959f5', N'operasyon-islemleri.firma-evrak-takibi.list', '2026-04-14T00:00:00.0000000Z', N'OperasyonIslemleri > FirmaEvrakTakibi > Listele yetkisi.', N'FirmaEvrakTakibi Listele', NULL);
                END

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [code] = N'operasyon-islemleri.firma-evrak-takibi.all-warehouses')
                   AND NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = '17e7f3cf-e687-2cb8-f6a5-0f76195d40aa')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('17e7f3cf-e687-2cb8-f6a5-0f76195d40aa', N'operasyon-islemleri.firma-evrak-takibi.all-warehouses', '2026-04-14T00:00:00.0000000Z', N'OperasyonIslemleri > FirmaEvrakTakibi > Tum Depolar yetkisi.', N'FirmaEvrakTakibi Tum Depolar', NULL);
                END

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT permission.[id], '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', '2026-04-14T00:00:00.0000000Z'
                FROM [app_permissions] AS permission
                WHERE permission.[code] IN (
                    N'operasyon-islemleri.firma-evrak-takibi.page',
                    N'operasyon-islemleri.firma-evrak-takibi.list',
                    N'operasyon-islemleri.firma-evrak-takibi.all-warehouses')
                  AND EXISTS (
                      SELECT 1 FROM [app_roles]
                      WHERE [id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [app_role_permissions] AS rolePermission
                      WHERE rolePermission.[permission_id] = permission.[id]
                        AND rolePermission.[role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE rolePermission
                FROM [app_role_permissions] AS rolePermission
                INNER JOIN [app_permissions] AS permission
                    ON permission.[id] = rolePermission.[permission_id]
                WHERE permission.[code] IN (
                    N'operasyon-islemleri.firma-evrak-takibi.page',
                    N'operasyon-islemleri.firma-evrak-takibi.list',
                    N'operasyon-islemleri.firma-evrak-takibi.all-warehouses');

                DELETE FROM [app_permissions]
                WHERE [code] IN (
                    N'operasyon-islemleri.firma-evrak-takibi.page',
                    N'operasyon-islemleri.firma-evrak-takibi.list',
                    N'operasyon-islemleri.firma-evrak-takibi.all-warehouses');
                """);
        }
    }
}
