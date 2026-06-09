using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260609143000_AddKasaHareketAktarimiPermissions")]
    public sealed class AddKasaHareketAktarimiPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = '89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4', N'kasa-islemleri.kasa-hareket-aktarimi.list', CAST('2026-04-14T00:00:00' AS datetime2), N'KasaIslemleri > KasaHareketAktarimi > Listele yetkisi.', N'KasaHareketAktarimi Listele', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = '06f08832-3250-fd22-a058-0ad3a5b3b491')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('06f08832-3250-fd22-a058-0ad3a5b3b491', N'kasa-islemleri.kasa-hareket-aktarimi.detail', CAST('2026-04-14T00:00:00' AS datetime2), N'KasaIslemleri > KasaHareketAktarimi > Detay yetkisi.', N'KasaHareketAktarimi Detay', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = 'da72621a-6d66-7862-f3e0-7d197b18c8a8')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('da72621a-6d66-7862-f3e0-7d197b18c8a8', N'kasa-islemleri.kasa-hareket-aktarimi.create', CAST('2026-04-14T00:00:00' AS datetime2), N'KasaIslemleri > KasaHareketAktarimi > Ekle yetkisi.', N'KasaHareketAktarimi Ekle', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = 'e8c3b7f0-c834-9c91-54a4-7734ca4e1644')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('e8c3b7f0-c834-9c91-54a4-7734ca4e1644', N'kasa-islemleri.kasa-hareket-aktarimi.update', CAST('2026-04-14T00:00:00' AS datetime2), N'KasaIslemleri > KasaHareketAktarimi > Guncelle yetkisi.', N'KasaHareketAktarimi Guncelle', NULL);
                END;
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = '89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4' AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', CAST('2026-04-14T00:00:00' AS datetime2));
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = '06f08832-3250-fd22-a058-0ad3a5b3b491' AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('06f08832-3250-fd22-a058-0ad3a5b3b491', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', CAST('2026-04-14T00:00:00' AS datetime2));
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = 'da72621a-6d66-7862-f3e0-7d197b18c8a8' AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('da72621a-6d66-7862-f3e0-7d197b18c8a8', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', CAST('2026-04-14T00:00:00' AS datetime2));
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = 'e8c3b7f0-c834-9c91-54a4-7734ca4e1644' AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('e8c3b7f0-c834-9c91-54a4-7734ca4e1644', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', CAST('2026-04-14T00:00:00' AS datetime2));
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [app_role_permissions]
                WHERE [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                  AND [permission_id] IN (
                    '89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4',
                    '06f08832-3250-fd22-a058-0ad3a5b3b491',
                    'da72621a-6d66-7862-f3e0-7d197b18c8a8',
                    'e8c3b7f0-c834-9c91-54a4-7734ca4e1644');
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM [app_permissions]
                WHERE [id] IN (
                    '89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4',
                    '06f08832-3250-fd22-a058-0ad3a5b3b491',
                    'da72621a-6d66-7862-f3e0-7d197b18c8a8',
                    'e8c3b7f0-c834-9c91-54a4-7734ca4e1644');
                """);
        }
    }
}
