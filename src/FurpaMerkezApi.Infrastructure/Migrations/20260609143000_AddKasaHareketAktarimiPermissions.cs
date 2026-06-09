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
                INSERT INTO "app_permissions" ("id", "code", "created_at_utc", "description", "name", "updated_at_utc")
                VALUES
                    ('89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4', 'kasa-islemleri.kasa-hareket-aktarimi.list', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'KasaIslemleri > KasaHareketAktarimi > Listele yetkisi.', 'KasaHareketAktarimi Listele', NULL),
                    ('06f08832-3250-fd22-a058-0ad3a5b3b491', 'kasa-islemleri.kasa-hareket-aktarimi.detail', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'KasaIslemleri > KasaHareketAktarimi > Detay yetkisi.', 'KasaHareketAktarimi Detay', NULL),
                    ('da72621a-6d66-7862-f3e0-7d197b18c8a8', 'kasa-islemleri.kasa-hareket-aktarimi.create', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'KasaIslemleri > KasaHareketAktarimi > Ekle yetkisi.', 'KasaHareketAktarimi Ekle', NULL),
                    ('e8c3b7f0-c834-9c91-54a4-7734ca4e1644', 'kasa-islemleri.kasa-hareket-aktarimi.update', TIMESTAMPTZ '2026-04-14 00:00:00+00', 'KasaIslemleri > KasaHareketAktarimi > Guncelle yetkisi.', 'KasaHareketAktarimi Guncelle', NULL)
                ON CONFLICT ("id") DO NOTHING;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "app_role_permissions" ("permission_id", "role_id", "assigned_at_utc")
                VALUES
                    ('89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('06f08832-3250-fd22-a058-0ad3a5b3b491', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('da72621a-6d66-7862-f3e0-7d197b18c8a8', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00'),
                    ('e8c3b7f0-c834-9c91-54a4-7734ca4e1644', '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a', TIMESTAMPTZ '2026-04-14 00:00:00+00')
                ON CONFLICT ("permission_id", "role_id") DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "app_role_permissions"
                WHERE "role_id" = '2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a'
                  AND "permission_id" IN (
                    '89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4',
                    '06f08832-3250-fd22-a058-0ad3a5b3b491',
                    'da72621a-6d66-7862-f3e0-7d197b18c8a8',
                    'e8c3b7f0-c834-9c91-54a4-7734ca4e1644');
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "app_permissions"
                WHERE "id" IN (
                    '89777b16-0fe4-34c6-0d7e-d1cc37b8b3d4',
                    '06f08832-3250-fd22-a058-0ad3a5b3b491',
                    'da72621a-6d66-7862-f3e0-7d197b18c8a8',
                    'e8c3b7f0-c834-9c91-54a4-7734ca4e1644');
                """);
        }
    }
}
