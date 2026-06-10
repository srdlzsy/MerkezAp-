using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    [DbContext(typeof(AuthDbContext))]
    [Migration("20260610100000_AddKasaCiroAktarimiPermissions")]
    public sealed class AddKasaCiroAktarimiPermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = '3eecc9d0-abaf-5712-2b42-c4fe5e8cd390')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('3eecc9d0-abaf-5712-2b42-c4fe5e8cd390', N'kasa-islemleri.kasa-ciro-aktarimi.list', CAST('2026-04-14T00:00:00' AS datetime2), N'KasaIslemleri > KasaCiroAktarimi > Listele yetkisi.', N'KasaCiroAktarimi Listele', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = '3447967a-13c8-7c73-2a68-4f5cf8eade1e')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('3447967a-13c8-7c73-2a68-4f5cf8eade1e', N'kasa-islemleri.kasa-ciro-aktarimi.detail', CAST('2026-04-14T00:00:00' AS datetime2), N'KasaIslemleri > KasaCiroAktarimi > Detay yetkisi.', N'KasaCiroAktarimi Detay', NULL);
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_permissions] WHERE [id] = '700c4d95-9708-9917-dfae-cc1a76bc9d31')
                BEGIN
                    INSERT INTO [app_permissions] ([id], [code], [created_at_utc], [description], [name], [updated_at_utc])
                    VALUES ('700c4d95-9708-9917-dfae-cc1a76bc9d31', N'kasa-islemleri.kasa-ciro-aktarimi.create', CAST('2026-04-14T00:00:00' AS datetime2), N'KasaIslemleri > KasaCiroAktarimi > Ekle yetkisi.', N'KasaCiroAktarimi Ekle', NULL);
                END;
                """);

            migrationBuilder.Sql(
                """
                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = '3eecc9d0-abaf-5712-2b42-c4fe5e8cd390' AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('3eecc9d0-abaf-5712-2b42-c4fe5e8cd390', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', CAST('2026-04-14T00:00:00' AS datetime2));
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = '3447967a-13c8-7c73-2a68-4f5cf8eade1e' AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('3447967a-13c8-7c73-2a68-4f5cf8eade1e', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', CAST('2026-04-14T00:00:00' AS datetime2));
                END;

                IF NOT EXISTS (SELECT 1 FROM [app_role_permissions] WHERE [permission_id] = '700c4d95-9708-9917-dfae-cc1a76bc9d31' AND [role_id] = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a')
                BEGIN
                    INSERT INTO [app_role_permissions] ([permission_id], [role_id], [assigned_at_utc])
                    VALUES ('700c4d95-9708-9917-dfae-cc1a76bc9d31', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', CAST('2026-04-14T00:00:00' AS datetime2));
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
                    '3eecc9d0-abaf-5712-2b42-c4fe5e8cd390',
                    '3447967a-13c8-7c73-2a68-4f5cf8eade1e',
                    '700c4d95-9708-9917-dfae-cc1a76bc9d31');
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM [app_permissions]
                WHERE [id] IN (
                    '3eecc9d0-abaf-5712-2b42-c4fe5e8cd390',
                    '3447967a-13c8-7c73-2a68-4f5cf8eade1e',
                    '700c4d95-9708-9917-dfae-cc1a76bc9d31');
                """);
        }
    }
}
