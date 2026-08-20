using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddB2BSettingsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @administratorRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';
                DECLARE @seededAtUtc datetime2 = '2026-04-14T00:00:00';

                DECLARE @permissions TABLE
                (
                    [id] uniqueidentifier NOT NULL,
                    [code] nvarchar(256) NOT NULL,
                    [name] nvarchar(200) NOT NULL,
                    [description] nvarchar(500) NOT NULL
                );

                INSERT INTO @permissions ([id], [code], [name], [description])
                VALUES
                    ('c72faca6-393d-9c67-14fd-2fae6c6a3b03', 'ayar-islemleri.b2b-ayarlari.manage', 'B2BAyarlari Yonet', 'AyarIslemleri > B2BAyarlari > Yonet yetkisi.'),
                    ('cf5485a9-9508-a460-396f-91625400f45a', 'ayar-islemleri.b2b-ayarlari.list', 'B2BAyarlari Listele', 'AyarIslemleri > B2BAyarlari > Listele yetkisi.'),
                    ('e1c743ee-c1a6-77f1-292b-f7ca5bad7029', 'ayar-islemleri.b2b-ayarlari.detail', 'B2BAyarlari Detay', 'AyarIslemleri > B2BAyarlari > Detay yetkisi.'),
                    ('e6d7fd49-7699-f9d1-5ac1-e981583be8b0', 'ayar-islemleri.b2b-ayarlari.create', 'B2BAyarlari Ekle', 'AyarIslemleri > B2BAyarlari > Ekle yetkisi.'),
                    ('9ab83e00-9719-78a3-0259-95bcfd709373', 'ayar-islemleri.b2b-ayarlari.update', 'B2BAyarlari Guncelle', 'AyarIslemleri > B2BAyarlari > Guncelle yetkisi.'),
                    ('5c63bf4f-9a6a-3086-8c95-49a96f03a97a', 'ayar-islemleri.b2b-ayarlari.delete', 'B2BAyarlari Sil', 'AyarIslemleri > B2BAyarlari > Sil yetkisi.'),
                    ('1efe5539-db47-e3c7-bf14-afc18946c23d', 'ayar-islemleri.b2b-ayarlari.all-warehouses', 'B2BAyarlari Tum Depolar', 'AyarIslemleri > B2BAyarlari > Tum Depolar yetkisi.');

                INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                SELECT source.[id], source.[code], @seededAtUtc, source.[description], source.[name], NULL
                FROM @permissions AS source
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions] AS existing
                    WHERE existing.[code] = source.[code]);

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT permission.[id], @administratorRoleId, @seededAtUtc
                FROM [app_permissions] AS permission
                JOIN @permissions AS source ON source.[code] = permission.[code]
                WHERE EXISTS (
                    SELECT 1
                    FROM [app_roles] AS role
                    WHERE role.[id] = @administratorRoleId)
                  AND NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existing
                    WHERE existing.[permission_id] = permission.[id]
                      AND existing.[role_id] = @administratorRoleId);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE rolePermission
                FROM [app_role_permissions] AS rolePermission
                JOIN [app_permissions] AS permission ON permission.[id] = rolePermission.[permission_id]
                WHERE permission.[code] LIKE 'ayar-islemleri.b2b-ayarlari.%';

                DELETE FROM [app_permissions]
                WHERE [code] LIKE 'ayar-islemleri.b2b-ayarlari.%';
                """);
        }
    }
}
