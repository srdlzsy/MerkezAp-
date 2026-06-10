using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    public partial class AddAyarIslemleriPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @seededAt datetime2 = CAST('2026-04-14T00:00:00' AS datetime2);
                DECLARE @administratorRoleId uniqueidentifier = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a';
                DECLARE @permissions TABLE
                (
                    [id] uniqueidentifier NOT NULL,
                    [code] nvarchar(100) NOT NULL,
                    [description] nvarchar(250) NOT NULL,
                    [name] nvarchar(100) NOT NULL
                );

                INSERT INTO @permissions ([id], [code], [description], [name])
                VALUES
                    ('2cefdb1e-6eeb-7f0e-6f6e-233745a0fd5f', N'ayar-islemleri.cihazlar.list', N'AyarIslemleri > Cihazlar > Listele yetkisi.', N'Cihazlar Listele'),
                    ('2b2f2255-fdd4-fd9d-aa7d-65ab7a9af0b8', N'ayar-islemleri.cihazlar.detail', N'AyarIslemleri > Cihazlar > Detay yetkisi.', N'Cihazlar Detay'),
                    ('75651541-38dd-2cc0-b03f-ad1bd7c27529', N'ayar-islemleri.cihazlar.create', N'AyarIslemleri > Cihazlar > Ekle yetkisi.', N'Cihazlar Ekle'),
                    ('f0227d56-7f05-7079-32a9-913207eeb3fe', N'ayar-islemleri.cihazlar.update', N'AyarIslemleri > Cihazlar > Guncelle yetkisi.', N'Cihazlar Guncelle'),
                    ('c634f806-6ea4-c911-863b-51d048dbe853', N'ayar-islemleri.sube-ayarlari.list', N'AyarIslemleri > SubeAyarlari > Listele yetkisi.', N'SubeAyarlari Listele'),
                    ('ed38e5da-0c08-e150-e338-12e07ecfdb03', N'ayar-islemleri.sube-ayarlari.detail', N'AyarIslemleri > SubeAyarlari > Detay yetkisi.', N'SubeAyarlari Detay'),
                    ('c895f593-6d77-d714-ed7f-fde9d84a79da', N'ayar-islemleri.sube-ayarlari.create', N'AyarIslemleri > SubeAyarlari > Ekle yetkisi.', N'SubeAyarlari Ekle'),
                    ('e47abdc4-2c27-d3bc-826f-e7fe89aab8bd', N'ayar-islemleri.sube-ayarlari.update', N'AyarIslemleri > SubeAyarlari > Guncelle yetkisi.', N'SubeAyarlari Guncelle'),
                    ('3652ece8-fdda-f63c-4997-f7a5d79dbb1f', N'ayar-islemleri.kasa-pos-terminalleri.list', N'AyarIslemleri > KasaPosTerminalleri > Listele yetkisi.', N'KasaPosTerminalleri Listele'),
                    ('0037eea6-6fa5-eb09-d83d-bd55266828ad', N'ayar-islemleri.kasa-pos-terminalleri.detail', N'AyarIslemleri > KasaPosTerminalleri > Detay yetkisi.', N'KasaPosTerminalleri Detay'),
                    ('c259eb88-a90a-b043-97a6-76b5efa631b1', N'ayar-islemleri.kasa-pos-terminalleri.create', N'AyarIslemleri > KasaPosTerminalleri > Ekle yetkisi.', N'KasaPosTerminalleri Ekle'),
                    ('64b5ea37-a134-2563-7bab-424f54b016dd', N'ayar-islemleri.kasa-pos-terminalleri.update', N'AyarIslemleri > KasaPosTerminalleri > Guncelle yetkisi.', N'KasaPosTerminalleri Guncelle'),
                    ('af64e806-3303-28af-125c-7fdda09d615a', N'ayar-islemleri.kasiyerler.list', N'AyarIslemleri > Kasiyerler > Listele yetkisi.', N'Kasiyerler Listele'),
                    ('d109be25-11bd-e2c0-4278-b3fd94a405d6', N'ayar-islemleri.kasiyerler.detail', N'AyarIslemleri > Kasiyerler > Detay yetkisi.', N'Kasiyerler Detay'),
                    ('5370bcc9-c42f-b7be-2101-ee97422c2bb6', N'ayar-islemleri.kasiyerler.create', N'AyarIslemleri > Kasiyerler > Ekle yetkisi.', N'Kasiyerler Ekle'),
                    ('e2722ce4-be38-3432-f333-0d71ea340839', N'ayar-islemleri.kasiyerler.update', N'AyarIslemleri > Kasiyerler > Guncelle yetkisi.', N'Kasiyerler Guncelle');

                INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                SELECT permission.[id], permission.[code], @seededAt, permission.[description], permission.[name], NULL
                FROM @permissions AS permission
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_permissions] AS existingPermission
                    WHERE existingPermission.[code] = permission.[code]);

                INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                SELECT existingPermission.[id], @administratorRoleId, @seededAt
                FROM @permissions AS permission
                INNER JOIN [app_permissions] AS existingPermission
                    ON existingPermission.[code] = permission.[code]
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [app_role_permissions] AS existingRolePermission
                    WHERE existingRolePermission.[permission_id] = existingPermission.[id]
                      AND existingRolePermission.[role_id] = @administratorRoleId);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @permissions TABLE ([code] nvarchar(100) NOT NULL);

                INSERT INTO @permissions ([code])
                VALUES
                    (N'ayar-islemleri.cihazlar.list'),
                    (N'ayar-islemleri.cihazlar.detail'),
                    (N'ayar-islemleri.cihazlar.create'),
                    (N'ayar-islemleri.cihazlar.update'),
                    (N'ayar-islemleri.sube-ayarlari.list'),
                    (N'ayar-islemleri.sube-ayarlari.detail'),
                    (N'ayar-islemleri.sube-ayarlari.create'),
                    (N'ayar-islemleri.sube-ayarlari.update'),
                    (N'ayar-islemleri.kasa-pos-terminalleri.list'),
                    (N'ayar-islemleri.kasa-pos-terminalleri.detail'),
                    (N'ayar-islemleri.kasa-pos-terminalleri.create'),
                    (N'ayar-islemleri.kasa-pos-terminalleri.update'),
                    (N'ayar-islemleri.kasiyerler.list'),
                    (N'ayar-islemleri.kasiyerler.detail'),
                    (N'ayar-islemleri.kasiyerler.create'),
                    (N'ayar-islemleri.kasiyerler.update');

                DELETE rolePermission
                FROM [app_role_permissions] AS rolePermission
                INNER JOIN [app_permissions] AS permission
                    ON permission.[id] = rolePermission.[permission_id]
                INNER JOIN @permissions AS permissionToDelete
                    ON permissionToDelete.[code] = permission.[code];

                DELETE permission
                FROM [app_permissions] AS permission
                INNER JOIN @permissions AS permissionToDelete
                    ON permissionToDelete.[code] = permission.[code];
                """);
        }
    }
}
