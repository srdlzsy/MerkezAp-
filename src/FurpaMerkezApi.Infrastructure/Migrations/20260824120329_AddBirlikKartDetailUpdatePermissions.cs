using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBirlikKartDetailUpdatePermissions : Migration
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
                    ('f889aa8b-89b4-5070-9346-f96ec796fe7c', 'kasa-islemleri.birlik-kart-sorgulama.detail', 'BirlikKartSorgulama Detay', 'KasaIslemleri > BirlikKartSorgulama > Detay yetkisi.'),
                    ('5ba314b0-49be-f1b3-c9cd-4b8f24790c32', 'kasa-islemleri.birlik-kart-sorgulama.update', 'BirlikKartSorgulama Guncelle', 'KasaIslemleri > BirlikKartSorgulama > Guncelle yetkisi.');

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
                WHERE permission.[code] IN (
                    'kasa-islemleri.birlik-kart-sorgulama.detail',
                    'kasa-islemleri.birlik-kart-sorgulama.update');

                DELETE FROM [app_permissions]
                WHERE [code] IN (
                    'kasa-islemleri.birlik-kart-sorgulama.detail',
                    'kasa-islemleri.birlik-kart-sorgulama.update');
                """);
        }
    }
}
